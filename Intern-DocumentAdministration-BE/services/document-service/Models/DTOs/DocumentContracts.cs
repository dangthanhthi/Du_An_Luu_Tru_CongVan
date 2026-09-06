namespace DocumentService;

public record CreateIncomingDocumentRequest(string Title, string? Summary, Guid? PartnerId, DateTime? ReceivedAt, List<Guid>? AttachmentFileIds);
public record CreateOutgoingDocumentRequest(string Title, string? Summary, Guid? PartnerId, Guid? SenderDepartmentId, List<Guid>? AttachmentFileIds);
public record CreateInternalDocumentRequest(string Title, string? Summary, Guid? SenderDepartmentId, List<Guid>? AttachmentFileIds);
public record CreateGeneralDocumentRequest(string Title, string? Summary, string? Direction, string? DocType, Guid? PartnerId, Guid? SenderDepartmentId, DateTime? ReceivedAt, List<Guid>? AttachmentFileIds);
public record UpdateDocumentRequest(string Title, string? Summary, Guid? PartnerId, Guid? SenderDepartmentId, DateTime? ReceivedAt);
public record ChangeStatusRequest(string Status, string? Note);
public record AssignAccessRequest(List<Guid> DepartmentIds);
public record AddAttachmentRequest(Guid FileId, string? AttachmentType);
public record DocumentFilter(string? SearchTerm, string? DocType, string? Status, Guid? PartnerId, Guid? DepartmentId, DateTime? FromDate, DateTime? ToDate, int PageNumber = 1, int PageSize = 10);

public record PagedResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface IDocumentBusinessService
{
    Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, Guid userId);
    Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, Guid userId);
    Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, Guid userId);
    Task<Document> UpdateAsync(Guid id, UpdateDocumentRequest req, Guid userId, string? userRole);
    Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, Guid userId, string? userRole);
    Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, Guid userId);
    Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req);
    Task RemoveAttachmentAsync(Guid id, Guid attachmentId);
    Task<Document?> GetByIdAsync(Guid id, Guid? userDepartmentId, string? userRole);
    Task<PagedResult<Document>> GetListAsync(DocumentFilter filter, Guid? userDepartmentId, string? userRole);
    Task<bool> DeleteAsync(Guid id, string? userRole);
}
