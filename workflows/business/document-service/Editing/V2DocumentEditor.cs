using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public sealed class V2DocumentEditor(DocumentDbContext db, TimeProvider clock)
{
    public async Task<Document> UpdateAsync(Guid documentId, V2EditDraft draft, V2EditorActor actor,
        V2EditTarget? target = null, CancellationToken ct = default, V2ReferenceSet? references = null, V2RelationScope? relationScope = null)
    {
        if (actor.UserId == Guid.Empty) throw Rule(401, "ACTOR_REQUIRED", "A verified actor is required.");
        if (!actor.IsActive) throw Rule(403, "ACTOR_INACTIVE", "The actor is inactive.");
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Editing must own a clean unit of work.");
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite())
            throw new NotSupportedException("Editing requires a relational database.");
        if (draft.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(draft.CompanyCode) ||
            string.IsNullOrWhiteSpace(draft.Subject) || draft.Subject.Trim().Length > 2000 || draft.Remark?.Length > 4000 ||
            draft.OriginatorUserId == Guid.Empty || draft.OwnerDepartmentId == Guid.Empty || draft.Sensitivity is not ("Normal" or "Confidential"))
            throw Rule(400, "INVALID_EDIT", "The edit draft is invalid.");
        draft = draft with { CompanyCode = draft.CompanyCode.Trim().ToUpperInvariant(), Subject = draft.Subject.Trim(),
            Remark = string.IsNullOrWhiteSpace(draft.Remark) ? null : draft.Remark.Trim(),
            Relations = draft.Relations is null ? null : V2DocumentRelations.Normalize(draft.Relations) };
        var operationId = Guid.NewGuid();
        var changedAt = clock.GetUtcNow();
        var attempts = 0;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                ct.ThrowIfCancellationRequested();
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (draft.Relations is not null) await V2MutationLocks.RelationsAsync(db, ct);
                await V2MutationLocks.DocumentAsync(db, documentId, ct);
                var header = await db.DocumentRegistrations.Include(x => x.Document).SingleOrDefaultAsync(x => x.DocumentId == documentId, ct)
                    ?? throw Rule(404, "DOCUMENT_NOT_FOUND", "The v2 document is unavailable.");
                var doc = header.Document ?? throw Rule(404, "DOCUMENT_NOT_FOUND", "The v2 document is unavailable.");
                await db.Entry(doc).Reference(x => x.KindDetails).LoadAsync(ct);
                await db.Entry(doc).Collection(x => x.Recipients).LoadAsync(ct);
                // Receipt lookup is limited to retries of this invocation, not a public edit-idempotency contract.
                if (attempts++ > 0 && await db.Set<DocumentEditAudit>().AsNoTracking().AnyAsync(x =>
                    x.Id == operationId && x.DocumentId == documentId && x.ActorUserId == actor.UserId && x.Version == header.Version, ct))
                {
                    await transaction.CommitAsync(ct);
                    return doc;
                }
                if (!V2EditAuthority.CanEdit(actor, header)) throw Rule(403, "EDIT_FORBIDDEN", "The actor cannot edit this document.");
                if (header.Version != draft.ExpectedVersion) throw Rule(409, "VERSION_CONFLICT", "The document has changed.");
                if (doc.DocType != header.Kind || doc.CreatedByUserId != header.InputterUserId || doc.SenderDepartmentId != header.OwnerDepartmentId)
                    throw Rule(409, "REGISTRATION_INCONSISTENT", "The document registration requires reconciliation.");

                var relations = new V2DocumentRelations(db);
                var relationPlan = draft.Relations is null ? null : await relations.PrepareAsync(doc, draft.Relations, actor.UserId, relationScope, false, ct);
                var before = State(doc, header, relationPlan is null ? null : JsonSerializer.Serialize(relationPlan.BeforeIds));
                var mapper = new V2KindDetailsMapper(db);
                var kindDetails = draft.Details is null ? null : await mapper.PrepareAsync(doc,
                    V2KindDetailsMapper.Normalize(header.Kind, draft.Details), references, ct);
                if (doc.Status == "Distributed") V2DistributionRules.Validate(header.Kind,
                    kindDetails?.Details ?? doc.KindDetails, kindDetails?.Recipients ?? doc.Recipients);
                var companyName = header.CompanyNameSnapshot;
                if (draft.CompanyCode != header.CompanyCode)
                {
                    var company = await db.BusinessCatalogEntries.AsNoTracking().SingleOrDefaultAsync(x =>
                        x.Group == "companies" && x.Code == draft.CompanyCode && x.IsActive, ct)
                        ?? throw Rule(400, "INVALID_COMPANY", "An active DAS company is required.");
                    if (string.IsNullOrWhiteSpace(company.Name) || company.Name.Length > 200)
                        throw Rule(400, "INVALID_COMPANY", "The company reference is invalid.");
                    companyName = company.Name;
                }
                var ownerCode = header.OwnerDepartmentCodeSnapshot;
                var ownerName = header.OwnerDepartmentNameSnapshot;
                if (draft.OwnerDepartmentId != header.OwnerDepartmentId || draft.OriginatorUserId != header.OriginatorUserId)
                {
                    if (target is null || target.OriginatorUserId != draft.OriginatorUserId || target.OwnerDepartmentId != draft.OwnerDepartmentId ||
                        !target.OriginatorIsActive || !target.DepartmentIsActive || string.IsNullOrWhiteSpace(target.OwnerDepartmentName) ||
                        target.OwnerDepartmentName.Trim().Length > 200 || string.IsNullOrWhiteSpace(target.OwnerDepartmentCode))
                        throw Rule(400, "INVALID_EDIT_TARGET", "Matching active originator/department references are required.");
                    if (!actor.DepartmentIds.Contains(draft.OwnerDepartmentId))
                        throw Rule(403, "DESTINATION_FORBIDDEN", "The target department is outside the actor's scope.");
                    ownerCode = target.OwnerDepartmentCode.Trim().ToUpperInvariant();
                    ownerName = target.OwnerDepartmentName.Trim();
                }
                string number;
                try
                {
                    // Validate the owner code even for Incoming, whose display format has no department segment.
                    _ = DocumentNumberFormatter.Format("OUTGOING", header.RegistrationDate, header.SequenceNumber, draft.CompanyCode, ownerCode);
                    number = DocumentNumberFormatter.Format(header.Kind, header.RegistrationDate, header.SequenceNumber,
                        draft.CompanyCode, header.Kind == "INCOMING" ? null : ownerCode);
                }
                catch (ArgumentException) { throw Rule(400, "INVALID_EDIT_REFERENCE", "The numbering references are invalid."); }

                var after = new EditState(draft.Subject, number, draft.CompanyCode, companyName, draft.OwnerDepartmentId,
                    ownerCode, ownerName, draft.OriginatorUserId, draft.Sensitivity, draft.IssuedDate, draft.Remark,
                    kindDetails is null ? before.KindDetails : V2KindDetailsMapper.Snapshot(kindDetails.Details, kindDetails.Recipients),
                    relationPlan is null ? null : JsonSerializer.Serialize(relationPlan.AfterIds));
                if (before == after) { await transaction.CommitAsync(ct); return doc; }
                if (header.Version == long.MaxValue) throw Rule(409, "VERSION_LIMIT", "The version cannot be advanced.");
                doc.Title = after.Subject; doc.DocumentNumber = after.RegistrationNumber; doc.SenderDepartmentId = after.OwnerDepartmentId;
                doc.UpdatedAt = changedAt.UtcDateTime;
                header.CompanyCode = after.CompanyCode; header.CompanyNameSnapshot = after.CompanyName;
                header.OwnerDepartmentId = after.OwnerDepartmentId; header.OwnerDepartmentCodeSnapshot = after.OwnerCode;
                header.OwnerDepartmentNameSnapshot = after.OwnerName; header.OriginatorUserId = after.OriginatorUserId;
                header.Sensitivity = after.Sensitivity; header.IssuedDate = after.IssuedDate; header.Remark = after.Remark;
                header.LastModifierUserId = actor.UserId; header.Version++;
                if (kindDetails is not null) mapper.Apply(doc, kindDetails);
                if (relationPlan is not null) relations.Apply(relationPlan, actor.UserId, changedAt);
                db.Set<DocumentEditAudit>().Add(new() { Id = operationId, DocumentId = documentId, ActorUserId = actor.UserId,
                    Version = header.Version, ChangedAt = changedAt, ChangesJson = Changes(before, after) });
                db.DocumentOutboxEvents.Add(new() { DocumentId = documentId, Type = "DocumentUpdated", AggregateVersion = header.Version,
                    CreatedAt = changedAt, PayloadJson = JsonSerializer.Serialize(new { documentId, version = header.Version }) });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return doc;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw Rule(409, "VERSION_CONFLICT", "The document has changed.");
        }
        catch { db.ChangeTracker.Clear(); throw; }
    }

    private sealed record EditState(string Subject, string RegistrationNumber, string CompanyCode, string CompanyName,
        Guid OwnerDepartmentId, string OwnerCode, string OwnerName, Guid OriginatorUserId, string Sensitivity, DateOnly? IssuedDate, string? Remark,
        string? KindDetails, string? RelatedDocumentIds);
    private static EditState State(Document d, DocumentRegistration h, string? relationIds) => new(d.Title, d.DocumentNumber, h.CompanyCode,
        h.CompanyNameSnapshot, h.OwnerDepartmentId, h.OwnerDepartmentCodeSnapshot, h.OwnerDepartmentNameSnapshot,
        h.OriginatorUserId, h.Sensitivity, h.IssuedDate, h.Remark, V2KindDetailsMapper.Snapshot(d.KindDetails, d.Recipients), relationIds);
    private static string Changes(EditState before, EditState after)
    {
        var old = JsonSerializer.SerializeToElement(before); var current = JsonSerializer.SerializeToElement(after);
        var changes = new Dictionary<string, object>();
        foreach (var property in old.EnumerateObject())
        {
            var value = current.GetProperty(property.Name);
            if (property.Value.GetRawText() == value.GetRawText()) continue;
            if (property.Name is nameof(EditState.KindDetails) or nameof(EditState.RelatedDocumentIds))
                changes[property.Name] = new { before = DetailsElement(property.Value), after = DetailsElement(value) };
            else changes[property.Name] = new { before = property.Value, after = value };
        }
        return JsonSerializer.Serialize(changes);
    }
    private static JsonElement? DetailsElement(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null :
        JsonSerializer.Deserialize<JsonElement>(value.GetString()!);
    private static DocumentRegistrationRuleException Rule(int status, string code, string message) => new(status, code, message);
}
