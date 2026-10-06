namespace AuthService.Organization;

public sealed class DirectoryProjectionState
{
    public string SourceId { get; set; } = "";
    public long Sequence { get; set; }
    public long AuthorizationRevision { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
}
public sealed class DirectoryInboxReceipt
{
    public string SourceId { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string Result { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
public sealed class DirectoryOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceId { get; set; } = "";
    public long AuthorizationRevision { get; set; }
    public long Sequence { get; set; }
    public string EventType { get; set; } = "DirectoryChanged";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
public enum DirectorySyncDisposition { Applied, Unchanged, Rejected, MessageConflict }
public sealed record DirectorySyncResult(DirectorySyncDisposition Disposition, long Sequence,
    long AuthorizationRevision, IReadOnlyList<OrganizationValidationError> Errors, bool IsDuplicate = false);
public sealed record PersistedDirectory(PreparedOrganizationDirectory Projection, long AuthorizationRevision, DateTimeOffset VerifiedAt);
public sealed class DirectoryUnavailableException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

