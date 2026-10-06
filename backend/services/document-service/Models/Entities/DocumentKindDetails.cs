using System.Text.Json.Serialization;

namespace DocumentService;

public sealed class DocumentKindDetails
{
    public Guid DocumentId { get; set; }
    public DateOnly? ReceivingDate { get; set; }
    public Guid? SenderPartnerId { get; set; }
    public string? SenderNameSnapshot { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? MethodCode { get; set; }
    public string? MethodNameSnapshot { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? DocumentTypeNameSnapshot { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryNameSnapshot { get; set; }
    public string? ContractNumber { get; set; }
    public string? OtherRecipients { get; set; }
    public string? Others { get; set; }
    [JsonIgnore] public Document? Document { get; set; }
}

public sealed class DocumentRecipient
{
    public Guid DocumentId { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public string NameSnapshot { get; set; } = "";
    [JsonIgnore] public Document? Document { get; set; }
}
