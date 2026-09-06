using System.Text.Json.Serialization;

namespace DocumentService;

public static class DocumentTypeConstants
{
    public const string INCOMING = "INCOMING";
    public const string OUTGOING = "OUTGOING";
    public const string INTERNAL = "INTERNAL";
}

public static class DocumentStatusConstants
{
    public const string Draft = "Draft";
    public const string Reviewed = "Reviewed";
    public const string Distributed = "Distributed";
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentNumber { get; set; } = default!;
    public string DocType { get; set; } = default!;
    public string Status { get; set; } = DocumentStatusConstants.Draft;
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? SenderDepartmentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? DistributedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<DocumentAttachment> Attachments { get; set; } = new();
    public List<DocumentDepartmentAccess> DepartmentAccesses { get; set; } = new();
    public List<DocumentStatusHistory> StatusHistories { get; set; } = new();
}

public class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid FileId { get; set; }
    public string? AttachmentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [JsonIgnore] public Document? Document { get; set; }
}

public class DocumentDepartmentAccess
{
    public Guid DocumentId { get; set; }
    public Guid DepartmentId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid AssignedByUserId { get; set; }
    [JsonIgnore] public Document? Document { get; set; }
}

public class DocumentStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = default!;
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    [JsonIgnore] public Document? Document { get; set; }
}

public class DocumentNumberCounter
{
    public string DocType { get; set; } = default!;
    public int Year { get; set; }
    public int CurrentValue { get; set; }
}
