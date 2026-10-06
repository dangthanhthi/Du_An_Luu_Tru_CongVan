namespace FilesService.Models.Entities;

// Durable intent precedes bytes and Files metadata, so it intentionally has no FK to Files.
public sealed class PdfUpload
{
    public Guid FileId { get; set; }
    public Guid UploaderUserId { get; set; }
    public string StorageKey { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string State { get; set; } = "Receiving";
    public string? FailureCode { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
    // Reserved for verified document claim. Bound files fail closed without live document policy.
    public Guid? DocumentId { get; set; }
}
