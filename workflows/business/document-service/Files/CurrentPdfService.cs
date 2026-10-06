using System.Data;
using System.Text.Json;
using Das.PdfProtocol;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record PdfReplaceDraft(Guid OperationId, Guid FileId, long ExpectedVersion);
public sealed record PdfReplaceResult(Guid OperationId, Guid DocumentId, Guid FileId, long Version);

public sealed class CurrentPdfService(DocumentDbContext db, IPdfFilesClient files, TimeProvider clock, IPdfAuthority? authority = null)
{
    public async Task<PdfReplaceResult> ReplaceAsync(Guid documentId, PdfReplaceDraft draft, V2EditorActor actor, CancellationToken ct = default)
    {
        if (actor.UserId == Guid.Empty) throw Rule(401,"ACTOR_REQUIRED");
        if (!actor.IsActive) throw Rule(403,"ACTOR_INACTIVE");
        if (documentId == Guid.Empty || draft.OperationId == Guid.Empty || draft.FileId == Guid.Empty || draft.ExpectedVersion < 1)
            throw Rule(400,"INVALID_PDF_REPLACEMENT");
        var operation = await Transaction(documentId, async () => {
            var h = await Header(documentId, ct); Authorize(h,actor);
            var existing = await db.Set<PdfReplacement>().SingleOrDefaultAsync(x => x.OperationId == draft.OperationId, ct);
            if (existing is not null) {
                if (existing.DocumentId != documentId || existing.FileId != draft.FileId || existing.ActorUserId != actor.UserId || existing.ExpectedVersion != draft.ExpectedVersion)
                    throw Rule(409,"PDF_OPERATION_CONFLICT");
                if (existing.State == "Aborted") throw Rule(409,"PDF_OPERATION_ABORTED");
                return existing;
            }
            Version(h,draft.ExpectedVersion);
            if (await db.Set<PdfReplacement>().AnyAsync(x => x.FileId == draft.FileId,ct)) throw Rule(409,"PDF_ALREADY_USED");
            var op = new PdfReplacement { OperationId = draft.OperationId, DocumentId = documentId, FileId = draft.FileId,
                ActorUserId = actor.UserId, ExpectedVersion = draft.ExpectedVersion, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Add(op); await db.SaveChangesAsync(ct); return op;
        },ct);
        if (operation.State == "Committed") return Result(operation);
        // Network work is outside the document transaction. A failed/lost response leaves
        // the durable Preparing receipt retryable; expiry first records terminal Aborted.
        var receipt = await files.PrepareAsync(new(draft.OperationId,documentId,draft.FileId,actor.UserId,draft.ExpectedVersion),ct);
        Validate(receipt,draft.OperationId,documentId,draft.FileId,actor.UserId);
        if (receipt.State is not ("Prepared" or "Active")) throw Rule(423,"PDF_UNAVAILABLE");
        var commitActor = authority is null ? actor : await authority.EditorAsync(documentId,actor.UserId,ct);
        if (commitActor.UserId != actor.UserId) throw Rule(503,"AUTHORITY_MISMATCH");
        return await Transaction(documentId, async () => {
            var h = await Header(documentId,ct); Authorize(h,commitActor);
            var op = await db.Set<PdfReplacement>().SingleAsync(x => x.OperationId == draft.OperationId,ct);
            if (op.State == "Committed") return Result(op);
            if (op.State != "Preparing") throw Rule(409,"PDF_OPERATION_ABORTED");
            Version(h,draft.ExpectedVersion);
            var current = await db.Set<DocumentCurrentPdf>().SingleOrDefaultAsync(x => x.DocumentId == documentId,ct);
            var oldId = current?.FileId;
            if (current is null) { current = new() { DocumentId = documentId }; db.Add(current); }
            else { if (current.Version == long.MaxValue) throw Rule(409,"VERSION_LIMIT"); current.Version++; }
            current.OperationId = draft.OperationId; current.FileId = draft.FileId; current.OriginalName = receipt.OriginalName;
            current.SizeBytes = receipt.SizeBytes; current.Sha256 = receipt.Sha256; current.State = "Pending";
            h.Version++; h.LastModifierUserId = actor.UserId; h.Document!.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            op.State = "Committed"; op.CommittedVersion = h.Version;
            db.Set<DocumentEditAudit>().Add(new() { Id = draft.OperationId, DocumentId = documentId, ActorUserId = actor.UserId,
                Version = h.Version, ChangedAt = clock.GetUtcNow(), ChangesJson = JsonSerializer.Serialize(new { CurrentPdf = new { before = oldId, after = draft.FileId } }) });
            db.DocumentOutboxEvents.Add(new() { DocumentId = documentId, Type = "PdfActivate", AggregateVersion = h.Version,
                CreatedAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { operationId = draft.OperationId }) });
            await db.SaveChangesAsync(ct); return Result(op);
        },ct);
    }

    public async Task<PdfOperation> OperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var op = await db.Set<PdfReplacement>().AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId,ct)
            ?? throw Rule(404,"PDF_OPERATION_NOT_FOUND");
        var current = await db.Set<DocumentCurrentPdf>().AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == op.DocumentId,ct);
        var state = op.State == "Committed" ? current?.OperationId == op.OperationId ? current.State == "Ready" ? "Current" : "Desired" : "Superseded" : op.State;
        return new(op.OperationId,op.DocumentId,op.FileId,state,current?.State == "Ready",op.ActorUserId,op.ExpectedVersion);
    }

    public async Task<int> DispatchAsync(CancellationToken ct = default)
    {
        var events = await db.DocumentOutboxEvents.AsNoTracking().Where(x => x.State == "Pending" && (x.Type == "PdfActivate" || x.Type == "PdfRetire"))
            .OrderBy(x => x.Attempts).ThenBy(x => x.AggregateVersion).ThenBy(x => x.Id).Take(100).ToArrayAsync(ct);
        var done = 0;
        foreach (var e in events) {
            try {
                var payload = JsonSerializer.Deserialize<OutboxPayload>(e.PayloadJson);
                if (payload is null || payload.operationId == Guid.Empty) throw Rule(400,"INVALID_PDF_EVENT");
                var id = payload.operationId;
                var proof = await OperationAsync(id,ct);
                if (proof.State == "Superseded") {
                    if (!proof.CurrentReady) continue;
                    await files.RetireAsync(id,ct);
                } else if (proof.State is "Desired" or "Current") {
                    if (e.Type == "PdfRetire") throw Rule(409,"CURRENT_PDF_PROTECTED");
                    var receipt = await files.ActivateAsync(id,ct);
                    if (receipt.State != "Active") throw Rule(423,"PDF_UNAVAILABLE");
                    await MarkReady(e.DocumentId,id,receipt,ct);
                } else { continue; }
                await db.DocumentOutboxEvents.Where(x => x.Id == e.Id && x.State == "Pending").ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.State,"Done").SetProperty(x => x.Attempts,x => x.Attempts + 1),ct);
                done++;
            } catch (Exception error) when (error is PdfProtocolException or DocumentRegistrationRuleException or IOException or DbUpdateException or JsonException) {
                db.ChangeTracker.Clear();
                await db.DocumentOutboxEvents.Where(x => x.Id == e.Id && x.State == "Pending" && x.Attempts < int.MaxValue).ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts,x => x.Attempts + 1),ct);
            }
        }
        return done;
    }

    private async Task MarkReady(Guid doc,Guid id,PdfReceipt receipt,CancellationToken ct)
    {
        await Transaction(doc,async () => {
            var current = await db.Set<DocumentCurrentPdf>().SingleOrDefaultAsync(x => x.DocumentId == doc,ct);
            if (current is null || current.OperationId != id) return false; // Superseded while remote activation was in flight.
            var op = await db.Set<PdfReplacement>().SingleAsync(x => x.OperationId == id,ct);
            Validate(receipt,id,doc,current.FileId,op.ActorUserId);
            if (receipt.Sha256 != current.Sha256 || receipt.SizeBytes != current.SizeBytes || receipt.OriginalName != current.OriginalName)
                throw Rule(409,"PDF_RECEIPT_CONFLICT");
            if (current.State == "Pending") { current.State = "Ready"; current.Version++; }
            else if (current.State != "Ready") throw Rule(423,"PDF_UNAVAILABLE");
            var old = await db.Set<PdfReplacement>().Where(x => x.DocumentId == doc && x.State == "Committed" && x.OperationId != id).ToArrayAsync(ct);
            foreach (var prior in old) {
                if (!await db.DocumentOutboxEvents.AnyAsync(x => x.DocumentId == doc && x.Type == "PdfRetire" && x.AggregateVersion == prior.CommittedVersion,ct))
                    db.DocumentOutboxEvents.Add(new() { DocumentId = doc, Type = "PdfRetire", AggregateVersion = prior.CommittedVersion!.Value,
                        CreatedAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { operationId = prior.OperationId }) });
            }
            await db.SaveChangesAsync(ct); return true;
        },ct);
    }

    public async Task<int> ExpirePreparingAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-2);
        var pending = await db.Set<PdfReplacement>().AsNoTracking().Where(x => x.State == "Preparing" && x.CreatedAt < cutoff).OrderBy(x => x.CreatedAt).Take(100).ToArrayAsync(ct);
        var count = 0;
        foreach (var old in pending.Where(x => x.CreatedAt < cutoff)) {
            await Transaction(old.DocumentId,async () => {
                var op = await db.Set<PdfReplacement>().SingleAsync(x => x.OperationId == old.OperationId,ct);
                if (op.State == "Preparing" && op.CreatedAt < cutoff) { op.State = "Aborted"; await db.SaveChangesAsync(ct); count++; }
                return true;
            },ct);
        }
        return count;
    }

    public async Task RefreshReadinessAsync(CancellationToken ct = default)
    {
        var current = await db.Set<DocumentCurrentPdf>().AsNoTracking().Where(x => x.State == "Ready").OrderBy(x => x.LastCheckedAt).ThenBy(x => x.DocumentId).Take(100).ToArrayAsync(ct);
        foreach (var link in current) {
            try {
                var receipt = await files.InspectAsync(link.OperationId,ct);
                if (receipt.OperationId != link.OperationId || receipt.DocumentId != link.DocumentId || receipt.FileId != link.FileId || receipt.Sha256 != link.Sha256 || receipt.SizeBytes != link.SizeBytes)
                    throw new PdfProtocolException(503,"PDF_RECEIPT_CONFLICT");
                if (receipt.State != "Missing") continue;
                await Transaction(link.DocumentId,async () => {
                    var saved = await db.Set<DocumentCurrentPdf>().SingleAsync(x => x.DocumentId == link.DocumentId,ct);
                    if (saved.OperationId == link.OperationId && saved.State == "Ready") { saved.State = "Missing"; saved.Version++; await db.SaveChangesAsync(ct); }
                    return true;
                },ct);
            } catch (PdfProtocolException) { /* Dependency outage is not proof of missing bytes. */ }
            finally {
                await db.Set<DocumentCurrentPdf>().Where(x => x.DocumentId == link.DocumentId && x.OperationId == link.OperationId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LastCheckedAt,clock.GetUtcNow().UtcDateTime),ct);
            }
        }
    }

    private async Task<DocumentRegistration> Header(Guid id,CancellationToken ct)
    {
        var h = await db.DocumentRegistrations.Include(x => x.Document).SingleOrDefaultAsync(x => x.DocumentId == id,ct);
        if (h?.Document is null || h.Document.IsDeleted) throw Rule(404,"DOCUMENT_NOT_FOUND");
        if (h.Document.Status is not ("InProgress" or "Distributed" or "Cancelled")) throw Rule(409,"DOCUMENT_RECONCILIATION_REQUIRED");
        if (h.Document.DocType != h.Kind || h.Document.CreatedByUserId != h.InputterUserId || h.Document.SenderDepartmentId != h.OwnerDepartmentId)
            throw Rule(409,"REGISTRATION_INCONSISTENT");
        return h;
    }
    private static void Authorize(DocumentRegistration h,V2EditorActor actor) {
        if (!actor.IsActive || !V2EditAuthority.CanEdit(actor,h)) throw Rule(403,"EDIT_FORBIDDEN");
    }
    private static void Version(DocumentRegistration h,long expected) {
        if (h.Version != expected) throw Rule(409,"VERSION_CONFLICT");
        if (h.Version == long.MaxValue) throw Rule(409,"VERSION_LIMIT");
    }
    private static PdfReplaceResult Result(PdfReplacement op) => new(op.OperationId,op.DocumentId,op.FileId,op.CommittedVersion!.Value);
    private static void Validate(PdfReceipt r,Guid op,Guid doc,Guid file,Guid actor) {
        if (r.OperationId != op || r.DocumentId != doc || r.FileId != file || r.UploaderUserId != actor || r.SizeBytes is < 1 or > 26214400 ||
            r.Sha256 is not { Length: 64 } || !r.Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(r.OriginalName) || r.OriginalName.Length > 200 || r.OriginalName.Any(char.IsControl))
            throw Rule(409,"PDF_RECEIPT_CONFLICT");
    }
    private async Task<T> Transaction<T>(Guid id,Func<Task<T>> body,CancellationToken ct)
    {
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) throw new InvalidOperationException("PDF replacement requires a clean unit of work.");
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite()) throw new NotSupportedException("PDF replacement requires a relational database.");
        try { return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await V2MutationLocks.DocumentAsync(db,id,ct);
            var result = await body(); await tx.CommitAsync(ct); return result;
        }); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Rule(409,"VERSION_CONFLICT"); }
        catch { db.ChangeTracker.Clear(); throw; }
    }
    private sealed record OutboxPayload(Guid operationId);
    private static DocumentRegistrationRuleException Rule(int status,string code) => new(status,code,"The current PDF operation is unavailable.");
}
