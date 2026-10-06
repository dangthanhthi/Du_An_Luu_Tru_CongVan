using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public sealed class V2DocumentLifecycle(DocumentDbContext db, TimeProvider clock)
{
    public async Task<Document> ChangeAsync(Guid documentId, V2StatusDraft draft, V2EditorActor actor, CancellationToken ct = default)
    {
        if (actor.UserId == Guid.Empty) throw Rule(401, "ACTOR_REQUIRED", "A verified actor is required.");
        if (!actor.IsActive) throw Rule(403, "ACTOR_INACTIVE", "The actor is inactive.");
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Lifecycle changes must own a clean unit of work.");
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite()) throw new NotSupportedException("Lifecycle changes require a relational database.");
        if (draft.ExpectedVersion < 1 || !Enum.IsDefined(draft.Action) || draft.Reason?.Length > 4000 ||
            (draft.Action == V2StatusAction.Cancel ? string.IsNullOrWhiteSpace(draft.Reason) : draft.Reason is not null))
            throw Rule(400, "INVALID_STATUS_INTENT", "The status intent is invalid.");
        draft = draft with { Reason = draft.Reason?.Trim() };
        var operationId = Guid.NewGuid(); var changedAt = clock.GetUtcNow(); var attempts = 0;
        try {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                ct.ThrowIfCancellationRequested(); db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                await V2MutationLocks.DocumentAsync(db, documentId, ct);
                var header = await db.DocumentRegistrations.Include(x => x.Document).SingleOrDefaultAsync(x => x.DocumentId == documentId, ct)
                    ?? throw Rule(404, "DOCUMENT_NOT_FOUND", "The v2 document is unavailable.");
                var doc = header.Document ?? throw Rule(404, "DOCUMENT_NOT_FOUND", "The v2 document is unavailable.");
                if (attempts++ > 0 && await db.Set<DocumentEditAudit>().AsNoTracking().AnyAsync(x =>
                    x.Id == operationId && x.DocumentId == documentId && x.ActorUserId == actor.UserId && x.Version == header.Version, ct)) {
                    await transaction.CommitAsync(ct); return doc;
                }
                var allowed = draft.Action == V2StatusAction.Restore ? V2EditAuthority.IsParticipant(actor, header) : V2EditAuthority.CanEdit(actor, header);
                if (!allowed) throw Rule(403, "STATUS_FORBIDDEN", "The actor cannot perform this status action.");
                if (header.Version != draft.ExpectedVersion) throw Rule(409, "VERSION_CONFLICT", "The document has changed.");
                if (doc.DocType != header.Kind || doc.CreatedByUserId != header.InputterUserId || doc.SenderDepartmentId != header.OwnerDepartmentId)
                    throw Rule(409, "REGISTRATION_INCONSISTENT", "The document registration requires reconciliation.");
                var cancellation = await db.Set<DocumentCancellation>().SingleOrDefaultAsync(x => x.DocumentId == documentId, ct);
                if (cancellation is { RestoredAt: null } && doc.Status != "Cancelled") throw Reconcile();
                var oldStatus = doc.Status; var before = CancellationState(cancellation);
                string newStatus;
                switch (draft.Action) {
                    case V2StatusAction.Distribute:
                        if (oldStatus is not ("InProgress" or "Distributed")) throw Transition();
                        newStatus = "Distributed"; break;
                    case V2StatusAction.Cancel:
                        if (oldStatus is not ("InProgress" or "Distributed")) throw Transition();
                        newStatus = "Cancelled"; break;
                    case V2StatusAction.Restore:
                        if (oldStatus != "Cancelled") throw Transition();
                        if (cancellation is null || cancellation.RestoredAt is not null || cancellation.PreviousStatus is not ("InProgress" or "Distributed")) throw Reconcile();
                        newStatus = cancellation.PreviousStatus; break;
                    default: throw Transition();
                }
                if (newStatus == "Distributed") {
                    await db.Entry(doc).Reference(x => x.KindDetails).LoadAsync(ct);
                    await db.Entry(doc).Collection(x => x.Recipients).LoadAsync(ct);
                    V2DistributionRules.Validate(header.Kind, doc.KindDetails, doc.Recipients);
                }
                if (oldStatus == newStatus) { await transaction.CommitAsync(ct); return doc; }
                if (header.Version == long.MaxValue) throw Rule(409, "VERSION_LIMIT", "The version cannot be advanced.");
                if (draft.Action == V2StatusAction.Cancel) {
                    if (cancellation is null) { cancellation = new() { DocumentId = documentId }; db.Set<DocumentCancellation>().Add(cancellation); }
                    cancellation.PreviousStatus = oldStatus; cancellation.Reason = draft.Reason!;
                    cancellation.CancelledAt = changedAt; cancellation.CancelledByUserId = actor.UserId;
                    cancellation.RestoredAt = null; cancellation.RestoredByUserId = null;
                } else if (draft.Action == V2StatusAction.Restore) {
                    cancellation!.RestoredAt = changedAt; cancellation.RestoredByUserId = actor.UserId;
                }
                doc.Status = newStatus; doc.UpdatedAt = changedAt.UtcDateTime;
                if (draft.Action == V2StatusAction.Distribute) doc.DistributedAt = changedAt.UtcDateTime;
                header.Version++; header.LastModifierUserId = actor.UserId;
                db.DocumentStatusHistory.Add(new() { DocumentId = documentId, OldStatus = oldStatus, NewStatus = newStatus,
                    ChangedByUserId = actor.UserId, ChangedAt = changedAt.UtcDateTime, Note = draft.Reason ?? draft.Action.ToString() });
                db.Set<DocumentEditAudit>().Add(new() { Id = operationId, DocumentId = documentId, ActorUserId = actor.UserId, Version = header.Version,
                    ChangedAt = changedAt, ChangesJson = JsonSerializer.Serialize(new {
                        Status = new { before = oldStatus, after = newStatus }, Cancellation = new { before, after = CancellationState(cancellation) } }) });
                db.DocumentOutboxEvents.Add(new() { DocumentId = documentId, Type = "DocumentStatusChanged", AggregateVersion = header.Version,
                    CreatedAt = changedAt, PayloadJson = JsonSerializer.Serialize(new { documentId, version = header.Version }) });
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return doc;
            });
        } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Rule(409, "VERSION_CONFLICT", "The document has changed."); }
        catch { db.ChangeTracker.Clear(); throw; }
    }
    private sealed record CancellationSnapshot(string PreviousStatus, string Reason, Guid CancelledByUserId, DateTimeOffset CancelledAt, Guid? RestoredByUserId, DateTimeOffset? RestoredAt);
    private static CancellationSnapshot? CancellationState(DocumentCancellation? c) => c is null ? null :
        new(c.PreviousStatus, c.Reason, c.CancelledByUserId, c.CancelledAt, c.RestoredByUserId, c.RestoredAt);
    private static DocumentRegistrationRuleException Transition() => Rule(409, "INVALID_STATUS_TRANSITION", "The current state does not allow this action.");
    private static DocumentRegistrationRuleException Reconcile() => Rule(409, "CANCELLATION_INCONSISTENT", "Cancellation provenance requires reconciliation.");
    private static DocumentRegistrationRuleException Rule(int status, string code, string message) => new(status, code, message);
}
