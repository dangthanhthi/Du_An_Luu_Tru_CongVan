using System.Text.Json.Serialization;

namespace DocumentService;

public sealed class DocumentRegistration
{
    public Guid DocumentId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly RegistrationDate { get; set; }
    public int RegistrationYear { get; set; }
    public int SequenceNumber { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public string CompanyCode { get; set; } = "";
    public string CompanyNameSnapshot { get; set; } = "";
    public Guid OwnerDepartmentId { get; set; }
    public string OwnerDepartmentCodeSnapshot { get; set; } = "";
    public string OwnerDepartmentNameSnapshot { get; set; } = "";
    public Guid InputterUserId { get; set; }
    public Guid OriginatorUserId { get; set; }
    public Guid LastModifierUserId { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string Sensitivity { get; set; } = "Normal";
    public string? Remark { get; set; }
    public long Version { get; set; } = 1;
    [JsonIgnore] public Document? Document { get; set; }
}

public sealed class RegistrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public string Kind { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string BodyHash { get; set; } = "";
    public Guid DocumentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    [JsonIgnore] public Document? Document { get; set; }
}

public sealed class DocumentOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Type { get; set; } = "DocumentRegistered";
    public string PayloadJson { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public long AggregateVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    [JsonIgnore] public Document? Document { get; set; }
}

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
