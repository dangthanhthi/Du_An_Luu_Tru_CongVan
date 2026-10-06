namespace DocumentService;

public record CreateIncomingDocumentRequest(string Title, string? Summary, Guid? PartnerId, DateTime? ReceivedAt, List<Guid>? AttachmentFileIds, string? SourceMessageId = null);
public record CreateOutgoingDocumentRequest(string Title, string? Summary, Guid PartnerId, Guid SenderDepartmentId, List<Guid>? AttachmentFileIds);
public record CreateInternalDocumentRequest(string Title, string? Summary, Guid SenderDepartmentId, List<Guid>? AttachmentFileIds);
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
    Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, DocumentActor actor);
    Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, DocumentActor actor);
    Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, DocumentActor actor);
    Task<Document> UpdateAsync(Guid id, UpdateDocumentRequest req, DocumentActor actor);
    Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, DocumentActor actor);
    Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, DocumentActor actor);
    Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req, DocumentActor actor);
    Task RemoveAttachmentAsync(Guid id, Guid attachmentId, DocumentActor actor);
    Task<Document?> GetByIdAsync(Guid id, DocumentActor actor);
    Task<PagedResult<Document>> GetListAsync(DocumentFilter filter, DocumentActor actor);
    Task<bool?> CanReadFileAsync(Guid fileId, DocumentActor actor);
    Task<bool> HasProcessedSourceMessageAsync(string sourceMessageId);
    Task<bool> DeleteAsync(Guid id, DocumentActor actor);
}
