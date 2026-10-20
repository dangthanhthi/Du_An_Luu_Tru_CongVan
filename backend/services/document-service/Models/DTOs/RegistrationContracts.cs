using System.Text.Json.Serialization;

namespace DocumentService;

public sealed record V2RegistrationDraft(string Kind, string CompanyCode, string Subject,
    Guid OriginatorUserId, Guid OwnerDepartmentId, string Sensitivity = "Normal", DateOnly? IssuedDate = null, string? Remark = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] V2KindDetailsDraft? Details = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<Guid>? RelatedDocumentIds = null);

// Trusted internal boundary, not an HTTP DTO. Caller must resolve/authorize identity
// from its provisioned authority before first registration; no EAP adapter is added here.
public sealed record RegistrationIdentity(Guid InputterUserId, Guid OriginatorUserId,
    Guid OwnerDepartmentId, string OwnerDepartmentCode, string OwnerDepartmentName);

public sealed class DocumentRegistrationRuleException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
