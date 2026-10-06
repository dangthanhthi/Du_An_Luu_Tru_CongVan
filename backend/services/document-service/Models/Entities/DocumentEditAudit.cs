namespace DocumentService;

public sealed class DocumentEditAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid ActorUserId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string ChangesJson { get; set; } = "";
}
