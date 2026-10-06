using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

// Resolves selected references to snapshots. It neither authorizes the actor nor grants access/delivery.
internal sealed class V2KindDetailsMapper(DocumentDbContext db)
{
    public static V2KindDetailsDraft Normalize(string kind, V2KindDetailsDraft draft)
    {
        DocumentNumberFormatter.ValidateKind(kind);
        var normalized = draft with {
            ReferenceNumber = Text(draft.ReferenceNumber, 200), ContractNumber = Text(draft.ContractNumber, 200),
            OtherRecipients = Text(draft.OtherRecipients, 4000), Others = Text(draft.Others, 4000),
            MethodCode = Code(draft.MethodCode), DocumentTypeCode = Code(draft.DocumentTypeCode), CategoryCode = Code(draft.CategoryCode),
            RecipientPartnerIds = Ids(draft.RecipientPartnerIds), DistributionTargetIds = Ids(draft.DistributionTargetIds)
        };
        var externalCount = normalized.RecipientPartnerIds!.Count;
        var targetCount = normalized.DistributionTargetIds!.Count;
        if (normalized.SenderPartnerId == Guid.Empty ||
            (kind == "INCOMING" && (normalized.SenderPartnerId is null || normalized.ReceivingDate is null || normalized.MethodCode is null || externalCount != 0 || normalized.OtherRecipients is not null)) ||
            (kind != "INCOMING" && (normalized.SenderPartnerId is not null || normalized.ReceivingDate is not null || normalized.ReferenceNumber is not null || targetCount != 0)) ||
            (kind == "INTERNAL" && (externalCount != 0 || normalized.MethodCode is not null || normalized.ContractNumber is not null || normalized.OtherRecipients is not null)))
            throw Invalid("The supplied details are incompatible with the document kind.");
        return normalized;
    }

    public async Task<Prepared> PrepareAsync(Document document, V2KindDetailsDraft draft,
        V2ReferenceSet? references, CancellationToken ct)
    {
        var old = document.KindDetails;
        var details = new DocumentKindDetails {
            DocumentId = document.Id, ReceivingDate = draft.ReceivingDate, SenderPartnerId = draft.SenderPartnerId,
            ReferenceNumber = draft.ReferenceNumber, MethodCode = draft.MethodCode, DocumentTypeCode = draft.DocumentTypeCode,
            CategoryCode = draft.CategoryCode, ContractNumber = draft.ContractNumber, OtherRecipients = draft.OtherRecipients, Others = draft.Others
        };
        details.MethodNameSnapshot = await CatalogName("methods", draft.MethodCode, old?.MethodCode, old?.MethodNameSnapshot, ct);
        details.DocumentTypeNameSnapshot = await CatalogName(document.DocType == "INTERNAL" ? "internalTypes" : "documentTypes",
            draft.DocumentTypeCode, old?.DocumentTypeCode, old?.DocumentTypeNameSnapshot, ct);
        details.CategoryNameSnapshot = await CatalogName("categories", draft.CategoryCode, old?.CategoryCode, old?.CategoryNameSnapshot, ct);
        if (draft.SenderPartnerId is { } sender)
            details.SenderNameSnapshot = old?.SenderPartnerId == sender && old.SenderNameSnapshot is not null
                ? old.SenderNameSnapshot : ExternalName(sender, references);
        var recipients = new List<DocumentRecipient>();
        var referenceType = document.DocType == "OUTGOING" ? "ExternalEntity" : "DistributionTarget";
        var ids = document.DocType == "OUTGOING" ? draft.RecipientPartnerIds! : draft.DistributionTargetIds!;
        var existing = document.Recipients.Where(x => x.ReferenceType == referenceType).ToDictionary(x => x.ReferenceId);
        var newTargetIds = referenceType == "DistributionTarget" ? ids.Where(x => !existing.ContainsKey(x)).ToArray() : [];
        var targets = newTargetIds.Length == 0 ? new Dictionary<Guid, DistributionTarget>() :
            await db.DistributionTargets.AsNoTracking().Where(x => newTargetIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        foreach (var id in ids)
        {
            string name;
            if (existing.TryGetValue(id, out var historical)) name = historical.NameSnapshot;
            else if (referenceType == "ExternalEntity") name = ExternalName(id, references);
            else if (targets.TryGetValue(id, out var target)) name = ReferenceName(target.Name);
            else throw Invalid("An active distribution target is required.");
            recipients.Add(new() { DocumentId = document.Id, ReferenceType = referenceType, ReferenceId = id, NameSnapshot = name });
        }
        return new(details, recipients);
    }

    public void Apply(Document document, Prepared prepared)
    {
        if (document.KindDetails is null) document.KindDetails = prepared.Details;
        else db.Entry(document.KindDetails).CurrentValues.SetValues(prepared.Details);
        var desired = prepared.Recipients.ToDictionary(x => (x.ReferenceType, x.ReferenceId));
        foreach (var old in document.Recipients.ToArray())
            if (!desired.ContainsKey((old.ReferenceType, old.ReferenceId)))
            {
                document.Recipients.Remove(old);
                db.Set<DocumentRecipient>().Remove(old);
            }
        var retained = document.Recipients.Select(x => (x.ReferenceType, x.ReferenceId)).ToHashSet();
        document.Recipients.AddRange(prepared.Recipients.Where(x => !retained.Contains((x.ReferenceType, x.ReferenceId))));
    }

    public static string? Snapshot(DocumentKindDetails? details, IEnumerable<DocumentRecipient> recipients) => details is null ? null :
        JsonSerializer.Serialize(new {
            details.ReceivingDate, details.SenderPartnerId, details.SenderNameSnapshot, details.ReferenceNumber,
            details.MethodCode, details.MethodNameSnapshot, details.DocumentTypeCode, details.DocumentTypeNameSnapshot,
            details.CategoryCode, details.CategoryNameSnapshot, details.ContractNumber, details.OtherRecipients, details.Others,
            Recipients = recipients.OrderBy(x => x.ReferenceType, StringComparer.Ordinal).ThenBy(x => x.ReferenceId)
                .Select(x => new { x.ReferenceType, x.ReferenceId, x.NameSnapshot }).ToArray()
        });

    internal sealed record Prepared(DocumentKindDetails Details, List<DocumentRecipient> Recipients);
    private async Task<string?> CatalogName(string group, string? code, string? oldCode, string? oldName, CancellationToken ct)
    {
        if (code is null) return null;
        if (code == oldCode && oldName is not null) return oldName;
        var name = await db.BusinessCatalogEntries.AsNoTracking().Where(x => x.Group == group && x.Code == code && x.IsActive)
            .Select(x => x.Name).SingleOrDefaultAsync(ct);
        return name is null ? throw Invalid("An active catalog selection is required.") : ReferenceName(name);
    }
    private static string ExternalName(Guid id, V2ReferenceSet? references)
    {
        if (references is null || !references.ExternalEntities.TryGetValue(id, out var reference) || reference.Id != id || !reference.IsActive)
            throw Invalid("An active verified external entity is required.");
        return ReferenceName(reference.Name);
    }
    private static string ReferenceName(string? value) => Text(value, 200) ?? throw Invalid("A reference name is required.");
    private static string? Text(string? value, int maximum)
    {
        if (value?.Length > maximum) throw Invalid("A details field exceeds its supported length.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static string? Code(string? value)
    {
        var code = Text(value, 64)?.ToUpperInvariant();
        if (code?.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') == true) throw Invalid("A catalog code is invalid.");
        return code;
    }
    private static Guid[] Ids(IReadOnlyList<Guid>? ids)
    {
        var copy = ids?.ToArray() ?? [];
        if (copy.Length > 200 || copy.Contains(Guid.Empty) || copy.Distinct().Count() != copy.Length)
            throw Invalid("Recipient IDs must be unique, nonempty and within the supported limit of 200.");
        return copy.Order().ToArray();
    }
    private static DocumentRegistrationRuleException Invalid(string message) => new(400, "INVALID_KIND_DETAILS", message);
}
