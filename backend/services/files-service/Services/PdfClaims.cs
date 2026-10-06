using System.Data;
using System.Security.Cryptography;
using Das.PdfProtocol;
using FilesService.Data;
using FilesService.Models.Entities;
using FilesService.Storage;
using Microsoft.EntityFrameworkCore;
namespace FilesService.Services;

public sealed class PdfClaims(FileDbContext db, IConfiguration config, IPdfDocumentClient documents, TimeProvider clock)
{
    private readonly PdfStorageOptions storage = PdfStorageOptions.Read(config);

    public async Task<PdfReceipt> PrepareAsync(PdfPrepare request, CancellationToken ct = default)
    {
        if (new[] { request.OperationId, request.DocumentId, request.FileId, request.UploaderUserId }.Contains(Guid.Empty) || request.ExpectedVersion < 1)
            throw Rule(400, "INVALID_CLAIM");
        var proof = await documents.OperationAsync(request.OperationId, ct);
        Match(proof, request.OperationId, request.DocumentId, request.FileId);
        if (proof.ActorUserId != request.UploaderUserId || proof.ExpectedVersion != request.ExpectedVersion) throw Rule(409,"CLAIM_PROOF_MISMATCH");
        if (proof.State is not ("Preparing" or "Desired" or "Current")) throw Rule(409, "CLAIM_ABORTED");
        return await Transaction(request.FileId, async () => {
            var old = await db.Set<PdfClaim>().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
            if (old is not null) {
                if (old.FileId != request.FileId || old.DocumentId != request.DocumentId || old.UploaderUserId != request.UploaderUserId || old.ExpectedVersion != request.ExpectedVersion)
                    throw Rule(409, "CLAIM_CONFLICT");
                if (old.State is not ("Prepared" or "Active")) throw Rule(409, "CLAIM_RETIRED");
                return await Receipt(old, ct);
            }
            if (await db.Set<PdfClaim>().AnyAsync(x => x.FileId == request.FileId, ct)) throw Rule(409, "FILE_ALREADY_CLAIMED");
            var upload = await db.Set<PdfUpload>().SingleOrDefaultAsync(x => x.FileId == request.FileId, ct);
            if (upload is null || upload.UploaderUserId != request.UploaderUserId || upload.DocumentId is not null)
                throw Rule(404, "FILE_NOT_FOUND");
            if (upload.State != "Available") throw Rule(423, "PDF_UNAVAILABLE");
            await VerifyBytes(upload, ct);
            var claim = new PdfClaim { OperationId = request.OperationId, FileId = request.FileId, DocumentId = request.DocumentId,
                UploaderUserId = request.UploaderUserId, ExpectedVersion = request.ExpectedVersion, CreatedAt = clock.GetUtcNow() };
            db.Add(claim); upload.DocumentId = request.DocumentId; upload.UpdatedAt = clock.GetUtcNow(); upload.Version++;
            await db.SaveChangesAsync(ct); return await Receipt(claim, ct);
        }, ct);
    }

    public async Task<PdfReceipt> ActivateAsync(Guid operationId, CancellationToken ct = default)
    {
        var known = await Load(operationId, ct); var proof = await documents.OperationAsync(operationId, ct);
        Match(proof, operationId, known.DocumentId, known.FileId);
        if (proof.ActorUserId != known.UploaderUserId || proof.ExpectedVersion != known.ExpectedVersion) throw Rule(409,"CLAIM_PROOF_MISMATCH");
        if (proof.State is not ("Desired" or "Current")) throw Rule(409, "CLAIM_NOT_CURRENT");
        return await Transaction(known.FileId, async () => {
            var claim = await LoadTracked(operationId, ct);
            if (claim.State is not ("Prepared" or "Active")) throw Rule(409, "CLAIM_RETIRED");
            var upload = await db.Set<PdfUpload>().SingleAsync(x => x.FileId == claim.FileId, ct);
            if (upload.DocumentId != claim.DocumentId || upload.State != "Available") throw Rule(423, "PDF_UNAVAILABLE");
            await VerifyBytes(upload, ct);
            if (claim.State == "Prepared") { claim.State = "Active"; claim.Version++; await db.SaveChangesAsync(ct); }
            return await Receipt(claim, ct);
        }, ct);
    }

    public async Task RetireAsync(Guid operationId, CancellationToken ct = default)
    {
        var known = await Load(operationId, ct);
        if (known.State == "Deleted") return;
        var proof = await documents.OperationAsync(operationId, ct);
        Match(proof, operationId, known.DocumentId, known.FileId);
        if (proof.ActorUserId != known.UploaderUserId || proof.ExpectedVersion != known.ExpectedVersion) throw Rule(409,"CLAIM_PROOF_MISMATCH");
        if (proof.State != "Aborted" && !(proof.State == "Superseded" && proof.CurrentReady)) throw Rule(409, "CURRENT_PDF_PROTECTED");
        await Transaction(known.FileId, async () => {
            var claim = await LoadTracked(operationId, ct);
            if (claim.State is not ("Retired" or "Deleted")) { claim.State = "Retired"; claim.Version++; await db.SaveChangesAsync(ct); }
            return true;
        }, ct);
        // Retired is durable before deletion. Retries never revive a retired claim.
        File.Delete(storage.PathFor(known.FileId));
        await Transaction(known.FileId, async () => {
            var claim = await LoadTracked(operationId, ct);
            if (claim.State == "Deleted") return true;
            if (claim.State != "Retired") throw Rule(409, "CLAIM_CONFLICT");
            var upload = await db.Set<PdfUpload>().SingleAsync(x => x.FileId == claim.FileId, ct);
            upload.State = "Failed"; upload.FailureCode = "PDF_RETIRED"; upload.Version++; upload.UpdatedAt = clock.GetUtcNow();
            claim.State = "Deleted"; claim.Version++; await db.SaveChangesAsync(ct); return true;
        }, ct);
    }

    public async Task<PdfReceipt> InspectAsync(Guid operationId, CancellationToken ct = default)
    {
        var claim = await Load(operationId, ct); var proof = await documents.OperationAsync(operationId, ct);
        Match(proof, operationId, claim.DocumentId, claim.FileId);
        if (proof.ActorUserId != claim.UploaderUserId || proof.ExpectedVersion != claim.ExpectedVersion) throw Rule(409,"CLAIM_PROOF_MISMATCH");
        if (proof.State is not ("Current" or "Desired")) throw Rule(404, "FILE_NOT_CURRENT");
        if (claim.State != "Active") return await Receipt(claim, ct);
        var upload = await db.Set<PdfUpload>().AsNoTracking().SingleAsync(x => x.FileId == claim.FileId, ct);
        if (upload.State == "Available") {
            try { await VerifyBytes(upload, ct); }
            catch (FileRuleException) {
                await db.Set<PdfUpload>().Where(x => x.FileId == upload.FileId && x.Version == upload.Version).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.State, "Missing").SetProperty(x => x.FailureCode, "STORAGE_INTEGRITY")
                    .SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
            }
        }
        var receipt = await Receipt(claim, ct);
        return receipt;
    }

    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var claims = await db.Set<PdfClaim>().AsNoTracking().Where(x => (x.State == "Prepared" || x.State == "Retired") && x.Version < long.MaxValue)
            .OrderBy(x => x.Version).ThenBy(x => x.OperationId).Take(100).ToArrayAsync(ct);
        var completed = 0;
        foreach (var claim in claims) {
            var id = claim.OperationId;
            try {
                var op = await documents.OperationAsync(id, ct);
                if (op.State == "Aborted" || op.State == "Superseded" && op.CurrentReady) { await RetireAsync(id, ct); completed++; }
                else if (op.State is "Desired" or "Current") { await ActivateAsync(id, ct); }
            } catch (Exception e) when (e is PdfProtocolException or FileRuleException or IOException or DbUpdateException) { db.ChangeTracker.Clear(); }
            finally {
                // Least-checked first keeps an unavailable old claim from starving later work.
                await db.Set<PdfClaim>().Where(x => x.OperationId == id && x.Version == claim.Version && (x.State == "Prepared" || x.State == "Retired"))
                    .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Version,x=>x.Version+1),ct);
            }
        }
        return completed;
    }

    private async Task VerifyBytes(PdfUpload upload, CancellationToken ct)
    {
        var file = await db.Files.AsNoTracking().SingleOrDefaultAsync(x => x.Id == upload.FileId, ct);
        if (file is null || file.SizeBytes != upload.SizeBytes || upload.StorageKey != upload.FileId.ToString("N") + ".pdf" ||
            upload.Sha256 is not { Length: 64 } || file.UploadedByUserId != upload.UploaderUserId) throw Rule(404, "FILE_NOT_FOUND");
        try {
            await using var stream = new FileStream(storage.PathFor(upload.FileId), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != upload.SizeBytes || Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)) != upload.Sha256)
                throw Rule(404, "PDF_INTEGRITY");
        } catch (IOException) { throw Rule(404, "PDF_INTEGRITY"); }
    }
    private async Task<PdfReceipt> Receipt(PdfClaim c, CancellationToken ct)
    {
        var u = await db.Set<PdfUpload>().AsNoTracking().SingleAsync(x => x.FileId == c.FileId, ct);
        return new(c.OperationId, c.DocumentId, c.FileId, c.UploaderUserId, u.OriginalName, u.SizeBytes!.Value, u.Sha256!, u.State == "Available" ? c.State : u.State);
    }
    private async Task<PdfClaim> Load(Guid id, CancellationToken ct) => await db.Set<PdfClaim>().AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == id, ct)
        ?? throw Rule(404, "CLAIM_NOT_FOUND");
    private async Task<PdfClaim> LoadTracked(Guid id, CancellationToken ct) => await db.Set<PdfClaim>().SingleAsync(x => x.OperationId == id, ct);
    private async Task<T> Transaction<T>(Guid id, Func<Task<T>> body, CancellationToken ct)
    {
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Claim requires a clean relational unit of work.");
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite()) throw new NotSupportedException("Claim requires SQL Server or SQLite.");
        try { return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsSqlServer()) await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource={"DAS:pdf:"+id.ToString("N")}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51004, 'PDF mutation lock unavailable.', 1;
                """, ct);
            var result = await body(); await tx.CommitAsync(ct); return result;
        }); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Rule(409,"CLAIM_CONFLICT"); }
        catch { db.ChangeTracker.Clear(); throw; }
    }
    private static void Match(PdfOperation op, Guid id, Guid doc, Guid file) {
        if (op.OperationId != id || op.DocumentId != doc || op.FileId != file) throw Rule(409,"CLAIM_PROOF_MISMATCH");
    }
    private static FileRuleException Rule(int status,string code) => new(status,code,"The managed PDF operation is unavailable.");
}
