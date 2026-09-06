namespace DocumentService;

public record PartnerDto(Guid Id, string Name, string Code, string EntityType, string? Email, string? Phone);
public record FileMetadataDto(Guid Id, string FileName, string FilePath, long FileSize, string ContentType);
public record SendNotificationRequest(string RecipientEmail, string Subject, string Body, Guid? RecipientUserId = null, Guid? RelatedDocumentId = null);

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
