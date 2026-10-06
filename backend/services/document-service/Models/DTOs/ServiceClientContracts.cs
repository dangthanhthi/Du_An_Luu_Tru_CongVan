namespace DocumentService;

public record PartnerDto(Guid Id, string FullName, string? ShortName, string EntityType, string? Email, string? Phone, bool IsActive);
public record FileMetadataDto(Guid Id, string OriginalName, long SizeBytes, string ContentType,
    string? State = null, bool CanDownload = false, string? Sha256 = null, bool CanAttach = false);
public record SendNotificationRequest(string RecipientEmail, string Subject, string Body);

public interface IPartnerServiceClient
{
    Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId);
}

public interface IFilesServiceClient
{
    Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId);
}

public interface INotificationServiceClient
{
    Task<bool> SendNotificationAsync(SendNotificationRequest request);
}
