namespace DocumentService;

public sealed class DocumentRelation
{
    public Guid IncomingDocumentId { get; set; }
    public Guid OutgoingDocumentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
