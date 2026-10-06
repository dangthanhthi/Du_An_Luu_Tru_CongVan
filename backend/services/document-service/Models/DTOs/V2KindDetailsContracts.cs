namespace DocumentService;

// Full kind-details replacement when present; null on an edit preserves current details.
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record V2KindDetailsDraft(DateOnly? ReceivingDate = null, Guid? SenderPartnerId = null,
    string? ReferenceNumber = null, string? MethodCode = null, string? DocumentTypeCode = null,
    string? CategoryCode = null, string? ContractNumber = null, string? OtherRecipients = null,
    string? Others = null, IReadOnlyList<Guid>? RecipientPartnerIds = null,
    IReadOnlyList<Guid>? DistributionTargetIds = null);

// Trusted partner-service facts. No browser endpoint accepts active flags or names as authority.
// Caller verifies IDs against its authoritative partner service; no EAP adapter is involved.
public sealed record ExternalEntityReference(Guid Id, string Name, bool IsActive);
public sealed record V2ReferenceSet(IReadOnlyDictionary<Guid, ExternalEntityReference> ExternalEntities);
