using System.Data;
using System.Security.Cryptography;
using Das.PdfProtocol;
using FilesService.Data;
using FilesService.Models.DTOs;
using FilesService.Models.Entities;
using FilesService.Storage;
using Microsoft.EntityFrameworkCore;
namespace FilesService.Services;

public sealed class FileStorageService : IFileStorageService
{
    private readonly FileDbContext db;
    private readonly PdfStorageOptions storage;
    private readonly IPdfThreatScanner scanner;
    private readonly TimeProvider clock;
    private readonly IPdfDocumentClient? documents;
    public FileStorageService(FileDbContext db, IConfiguration config, IPdfThreatScanner? scanner = null, TimeProvider? clock = null, IPdfDocumentClient? documents = null)
    { this.db = db; storage = PdfStorageOptions.Read(config); this.scanner = scanner ?? new UnavailablePdfThreatScanner(); this.clock = clock ?? TimeProvider.System; this.documents = documents; }

    public async Task<FileRecord> UploadFileAsync(IFormFile file, Guid userId, CancellationToken ct = default)
    {
        Actor(userId); CleanUnit();
        if (file is null || file.Length < 1) throw Rule(400, "EMPTY_FILE", "A nonempty file is required.");
        if (file.Length > storage.MaxBytes) throw Rule(413, "PDF_TOO_LARGE", "The PDF exceeds the configured byte limit.");
        var name = Path.GetFileName(file.FileName.Replace('\\','/')).Trim();
        if (name.Length is < 1 or > 200 || name.Any(char.IsControl) || !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw Rule(415, "PDF_REQUIRED", "Use a PDF filename without control characters, at most 200 characters.");
        var id = Guid.NewGuid(); var createdAt = clock.GetUtcNow(); var temp = storage.PathFor(id, true); var final = storage.PathFor(id);
        try { await PersistIntent(id, userId, name, createdAt, ct); }
        catch { db.ChangeTracker.Clear(); throw; }
        long size = 0; string hash; var renamed = false;
        try {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var source = file.OpenReadStream())
            await using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous)) {
                var buffer = new byte[65536];
                while (true) {
                    var read = await source.ReadAsync(buffer, ct); if (read == 0) break;
                    if (size > storage.MaxBytes - read) throw Rule(413, "PDF_TOO_LARGE", "The PDF exceeds the configured byte limit.");
                    size += read; sha.AppendData(buffer, 0, read); await target.WriteAsync(buffer.AsMemory(0,read), ct);
                }
                if (size == 0) throw Rule(400, "EMPTY_FILE", "A nonempty file is required.");
                await target.FlushAsync(ct); target.Flush(true); new PdfValidationService().Validate(target, ct);
            }
            hash = Convert.ToHexString(sha.GetHashAndReset());
            PdfScanResult scan;
            await using (var read = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                try { scan = await scanner.ScanAsync(read, ct); }
                catch (OperationCanceledException) { throw; }
                catch { scan = PdfScanResult.Unavailable; }
            }
            if (scan == PdfScanResult.Malicious) throw Rule(422, "PDF_REJECTED", "The PDF was rejected by content scanning.");
            var state = scan == PdfScanResult.Clean ? "Available" : "PendingScan";
            ct.ThrowIfCancellationRequested(); File.Move(temp, final, false); renamed = true;
            return await FinalizeMetadata(id, userId, name, final, size, hash, state, createdAt, ct);
        } catch (Exception error) {
            db.ChangeTracker.Clear();
            if (!renamed) {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { /* Intent tracks staged bytes. */ }
                // Final DB failure may actually have committed: do not delete finalized bytes
                // or overwrite a committed receipt. Reconciliation owns that boundary.
                try {
                    await db.Set<PdfUpload>().Where(x => x.FileId == id && x.State == "Receiving").ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.State, error is FileRuleException ? "Rejected" : "Failed")
                        .SetProperty(x => x.FailureCode, error is FileRuleException ? ((FileRuleException)error).Code : "UPLOAD_INTERRUPTED")
                        .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()).SetProperty(x => x.Version, x => x.Version + 1), CancellationToken.None);
                } catch { /* Original error wins; Receiving intent is still discoverable. */ }
            }
            throw;
        }
    }
    private async Task PersistIntent(Guid id, Guid actor, string name, DateTimeOffset createdAt, CancellationToken ct)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existing = await db.Set<PdfUpload>().SingleOrDefaultAsync(x => x.FileId == id, ct);
            if (existing is null) {
                db.Set<PdfUpload>().Add(new() { FileId = id, UploaderUserId = actor, OriginalName = name,
                    StorageKey = id.ToString("N") + ".pdf", CreatedAt = createdAt, UpdatedAt = createdAt }); await db.SaveChangesAsync(ct);
            } else if (existing.UploaderUserId != actor || existing.OriginalName != name) throw Rule(409, "UPLOAD_CONFLICT", "The upload intent conflicts.");
            await tx.CommitAsync(ct);
        });
    }
    private async Task<FileRecord> FinalizeMetadata(Guid id, Guid actor, string name, string path, long size, string hash, string state, DateTimeOffset createdAt, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var intent = await db.Set<PdfUpload>().SingleAsync(x => x.FileId == id, ct); var saved = await db.Files.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (saved is not null && intent.State == state && intent.Sha256 == hash && intent.SizeBytes == size && intent.UploaderUserId == actor) {
                await tx.CommitAsync(ct); return saved;
            }
            if (saved is not null || intent.State != "Receiving" || intent.UploaderUserId != actor) throw Rule(409, "UPLOAD_CONFLICT", "The upload intent conflicts.");
            var record = new FileRecord { Id = id, OriginalName = name, StoragePath = path, ContentType = "application/pdf", SizeBytes = size,
                UploadedByUserId = actor, CreatedAt = createdAt.UtcDateTime };
            db.Files.Add(record); intent.SizeBytes = size; intent.Sha256 = hash; intent.State = state;
            intent.FailureCode = state == "PendingScan" ? "SCANNER_UNAVAILABLE" : null; intent.UpdatedAt = clock.GetUtcNow(); intent.Version++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return record;
        });
    }
    public async Task<ManagedFileInfo> GetFileInfoAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var (record, intent) = await FindOwned(id, userId, ct);
        return new(record.Id, record.OriginalName, "application/pdf", record.SizeBytes, intent.Sha256!, intent.State, intent.State == "Available", intent.State == "Available" && intent.DocumentId is null);
    }
    public async Task<(Stream fileStream, string contentType, string fileName, string fileHash)> DownloadFileAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var (record, intent) = await FindOwned(id, userId, ct);
        if (intent.State != "Available") throw Rule(423, "PDF_UNAVAILABLE", "The PDF is not available for content access.");
        var path = storage.PathFor(id); FileStream? stream = null;
        try {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (stream.Length != record.SizeBytes || Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)) != intent.Sha256)
                throw new IOException("Managed PDF integrity failure.");
            stream.Position = 0; return (stream, "application/pdf", record.OriginalName, intent.Sha256!);
        } catch (IOException) {
            if (stream is not null) await stream.DisposeAsync();
            await db.Set<PdfUpload>().Where(x => x.FileId == id && x.Version == intent.Version && x.State == "Available").ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, "Missing").SetProperty(x => x.FailureCode, "STORAGE_INTEGRITY")
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()).SetProperty(x => x.Version, x => x.Version + 1), ct);
            throw Rule(404, "FILE_NOT_FOUND", "The managed PDF is unavailable.");
        } catch { if (stream is not null) await stream.DisposeAsync(); throw; }
    }
    private async Task<(FileRecord Record, PdfUpload Intent)> FindOwned(Guid id, Guid actor, CancellationToken ct)
    {
        Actor(actor);
        var intent = await db.Set<PdfUpload>().AsNoTracking().SingleOrDefaultAsync(x => x.FileId == id, ct);
        if (intent is null) throw Rule(404,"FILE_NOT_FOUND","The managed PDF is unavailable.");
        if (intent.DocumentId is null) {
            if (intent.UploaderUserId != actor) throw Rule(404,"FILE_NOT_FOUND","The managed PDF is unavailable.");
        } else {
            var claim = await db.Set<PdfClaim>().AsNoTracking().SingleOrDefaultAsync(x => x.FileId == id && x.DocumentId == intent.DocumentId && x.State == "Active", ct);
            if (claim is null) throw Rule(404,"FILE_NOT_FOUND","The managed PDF is unavailable.");
            if (documents is null) throw Rule(503,"DOCUMENT_AUTH_UNAVAILABLE","Document authorization is unavailable.");
            bool allowed;
            try { allowed = await documents.CanReadAsync(claim.DocumentId,id,claim.OperationId,actor,ct); }
            catch (PdfProtocolException) { throw Rule(503,"DOCUMENT_AUTH_UNAVAILABLE","Document authorization is unavailable."); }
            if (!allowed) throw Rule(404,"FILE_NOT_FOUND","The managed PDF is unavailable.");
        }
        var record = await db.Files.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UploadedByUserId == intent.UploaderUserId, ct);
        if (intent is null || record is null || intent.StorageKey != id.ToString("N") + ".pdf" || intent.SizeBytes != record.SizeBytes ||
            intent.Sha256?.Length != 64 || intent.State is not ("Available" or "PendingScan" or "Missing")) throw Rule(404, "FILE_NOT_FOUND", "The managed PDF is unavailable.");
        return (record, intent);
    }
    private void CleanUnit()
    {
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Upload must own a clean unit of work.");
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite()) throw new NotSupportedException("Upload requires a relational database.");
    }
    private static void Actor(Guid id) { if (id == Guid.Empty) throw Rule(401, "ACTOR_REQUIRED", "A verified uploader is required."); }
    private static FileRuleException Rule(int status, string code, string message) => new(status, code, message);
}
