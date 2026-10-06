namespace FilesService.Models.Entities;

public sealed class PdfClaim
{
    public Guid OperationId { get; set; }
    public Guid FileId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid UploaderUserId { get; set; }
    public long ExpectedVersion { get; set; }
    public string State { get; set; } = "Prepared";
    public DateTimeOffset CreatedAt { get; set; }
    public long Version { get; set; } = 1;
}
