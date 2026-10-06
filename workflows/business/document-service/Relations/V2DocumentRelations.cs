using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public sealed class V2DocumentRelations(DocumentDbContext db)
{
    public async Task<IReadOnlyList<Guid>> GetRelatedIdsAsync(Guid documentId, V2EditorActor actor, V2RelationScope scope,
        CancellationToken ct = default)
    {
        if (actor.UserId == Guid.Empty) throw Rule(401, "ACTOR_REQUIRED");
        if (!actor.IsActive) throw Rule(403, "ACTOR_INACTIVE");
        ValidateScope(actor.UserId, scope, [documentId]);
        var header = await db.DocumentRegistrations.AsNoTracking().Include(x => x.Document).SingleOrDefaultAsync(x => x.DocumentId == documentId, ct)
            ?? throw Rule(404, "DOCUMENT_NOT_FOUND");
        if (header.Document is null) throw Rule(404, "DOCUMENT_NOT_FOUND");
        if (header.Kind == "INTERNAL") return [];
        var kind = Opposite(header.Kind);
        var readable = scope.ReadableDocumentIds.ToArray();
        var ids = db.Set<DocumentRelation>().Where(x => x.IncomingDocumentId == documentId || x.OutgoingDocumentId == documentId)
            .Select(x => x.IncomingDocumentId == documentId ? x.OutgoingDocumentId : x.IncomingDocumentId);
        // Scope and non-deleted v2 resource checks occur in the query, before returning any IDs.
        return await db.DocumentRegistrations.AsNoTracking().Where(x => ids.Contains(x.DocumentId) && readable.Contains(x.DocumentId) &&
            x.Kind == kind && x.Document != null && x.Document.DocType == x.Kind).Select(x => x.DocumentId).OrderBy(x => x).ToListAsync(ct);
    }

    internal static V2RelationChange Normalize(V2RelationChange change)
    {
        var added = Ids(change.AddedIds); var removed = Ids(change.RemovedIds);
        if (added.Intersect(removed).Any()) throw Rule(400, "CONTRADICTORY_RELATIONS");
        return new(added, removed);
    }
    internal static Guid[]? NormalizeRegistration(string kind, IReadOnlyList<Guid>? ids)
    {
        var normalized = Ids(ids);
        if (normalized.Length == 0) return null; // Keep previous header-only receipt hashes unchanged.
        _ = Opposite(kind);
        return normalized;
    }
    internal static void ValidateScope(Guid actor, V2RelationScope? scope, IEnumerable<Guid> ids)
    {
        if (scope is null || scope.ActorUserId != actor) throw Rule(403, "RELATION_AUTHORITY_REQUIRED");
        if (ids.Any(x => !scope.ReadableDocumentIds.Contains(x))) throw Rule(404, "DOCUMENT_NOT_FOUND");
    }

    // Caller owns a serializable transaction and the relation lock before the source aggregate lock.
    internal async Task<Plan> PrepareAsync(Document source, V2RelationChange change, Guid actor,
        V2RelationScope? scope, bool registering, CancellationToken ct)
    {
        var expectedKind = Opposite(source.DocType);
        var referenced = change.AddedIds!.Concat(change.RemovedIds!).Distinct().Order().ToArray();
        if (referenced.Contains(source.Id)) throw Rule(400, "SELF_RELATION");
        ValidateScope(actor, scope, registering ? referenced : referenced.Append(source.Id));
        foreach (var id in referenced) await V2MutationLocks.DocumentAsync(db, id, ct);
        var targets = await db.DocumentRegistrations.Include(x => x.Document).Where(x => referenced.Contains(x.DocumentId)).ToDictionaryAsync(x => x.DocumentId, ct);
        foreach (var id in referenced)
        {
            if (!targets.TryGetValue(id, out var header) || header.Document is null) throw Rule(404, "DOCUMENT_NOT_FOUND");
            if (header.Kind != expectedKind) throw Rule(400, "INVALID_RELATION_KIND");
            if (header.Document.DocType != header.Kind || header.Document.CreatedByUserId != header.InputterUserId || header.Document.SenderDepartmentId != header.OwnerDepartmentId)
                throw Rule(409, "REGISTRATION_INCONSISTENT");
        }
        var edges = registering ? [] : await db.Set<DocumentRelation>().Where(x => x.IncomingDocumentId == source.Id || x.OutgoingDocumentId == source.Id).ToListAsync(ct);
        var before = edges.Select(x => Other(x, source.Id)).Order().ToArray();
        var current = before.ToHashSet();
        var added = change.AddedIds!.Where(x => !current.Contains(x)).ToArray();
        var removed = change.RemovedIds!.Where(current.Contains).ToArray();
        var after = current.Except(removed).Concat(added).Order().ToArray();
        var changed = added.Concat(removed).ToArray();
        var otherEdges = changed.Length == 0 ? [] : await db.Set<DocumentRelation>().AsNoTracking()
            .Where(x => changed.Contains(x.IncomingDocumentId) || changed.Contains(x.OutgoingDocumentId)).ToListAsync(ct);
        var targetChanges = new List<TargetChange>();
        foreach (var id in changed)
        {
            var header = targets[id];
            if (header.Version == long.MaxValue) throw Rule(409, "VERSION_LIMIT");
            var oldIds = otherEdges.Where(x => x.IncomingDocumentId == id || x.OutgoingDocumentId == id).Select(x => Other(x, id)).Order().ToArray();
            var newIds = added.Contains(id) ? oldIds.Append(source.Id).Order().ToArray() : oldIds.Where(x => x != source.Id).ToArray();
            targetChanges.Add(new(header, oldIds, newIds));
        }
        return new(before, after, edges.Where(x => removed.Contains(Other(x, source.Id))).ToArray(),
            added.Select(id => new DocumentRelation { IncomingDocumentId = source.DocType == "INCOMING" ? source.Id : id,
                OutgoingDocumentId = source.DocType == "OUTGOING" ? source.Id : id }).ToArray(), targetChanges);
    }

    internal void Apply(Plan plan, Guid actor, DateTimeOffset changedAt)
    {
        db.Set<DocumentRelation>().RemoveRange(plan.Removed);
        foreach (var edge in plan.Added) { edge.CreatedByUserId = actor; edge.CreatedAt = changedAt; }
        db.Set<DocumentRelation>().AddRange(plan.Added);
        foreach (var change in plan.Targets)
        {
            var header = change.Header; header.Version++; header.LastModifierUserId = actor;
            header.Document!.UpdatedAt = changedAt.UtcDateTime;
            db.Set<DocumentEditAudit>().Add(new() { DocumentId = header.DocumentId, ActorUserId = actor, Version = header.Version,
                ChangedAt = changedAt, ChangesJson = JsonSerializer.Serialize(new { RelatedDocumentIds = new { before = change.BeforeIds, after = change.AfterIds } }) });
            db.DocumentOutboxEvents.Add(new() { DocumentId = header.DocumentId, Type = "DocumentRelationsChanged", AggregateVersion = header.Version,
                CreatedAt = changedAt, PayloadJson = JsonSerializer.Serialize(new { documentId = header.DocumentId, version = header.Version }) });
        }
    }

    internal sealed record Plan(Guid[] BeforeIds, Guid[] AfterIds, DocumentRelation[] Removed, DocumentRelation[] Added, List<TargetChange> Targets);
    internal sealed record TargetChange(DocumentRegistration Header, Guid[] BeforeIds, Guid[] AfterIds);
    private static Guid Other(DocumentRelation edge, Guid id) => edge.IncomingDocumentId == id ? edge.OutgoingDocumentId : edge.IncomingDocumentId;
    private static Guid[] Ids(IReadOnlyList<Guid>? ids)
    {
        var copy = ids?.ToArray() ?? [];
        if (copy.Length > 200 || copy.Contains(Guid.Empty) || copy.Distinct().Count() != copy.Length) throw Rule(400, "INVALID_RELATION_IDS");
        return copy.Order().ToArray();
    }
    private static string Opposite(string kind) => kind switch { "INCOMING" => "OUTGOING", "OUTGOING" => "INCOMING", _ => throw Rule(400, "INVALID_RELATION_KIND") };
    private static DocumentRegistrationRuleException Rule(int status, string code) => new(status, code, "The document relation operation cannot be completed.");
}
