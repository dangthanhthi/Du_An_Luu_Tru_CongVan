namespace DocumentService;

public sealed class DocumentCurrentPdf
{
    public Guid DocumentId { get; set; }
    public Guid OperationId { get; set; }
    public Guid FileId { get; set; }
    public string OriginalName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string State { get; set; } = "Pending";
    public long Version { get; set; } = 1;
    public DateTime LastCheckedAt { get; set; } = DateTime.UnixEpoch;
}
public sealed class PdfReplacement
{
    public Guid OperationId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid FileId { get; set; }
    public Guid ActorUserId { get; set; }
    public long ExpectedVersion { get; set; }
    public long? CommittedVersion { get; set; }
    public string State { get; set; } = "Preparing";
    public DateTime CreatedAt { get; set; }
}
