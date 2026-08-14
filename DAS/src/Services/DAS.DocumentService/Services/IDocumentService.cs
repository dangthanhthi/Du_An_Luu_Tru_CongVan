using System;
using System.Threading.Tasks;
using DAS.DocumentService.DTOs;

namespace DAS.DocumentService.Services
{
    public interface IDocumentService
    {
        Task<DocumentResponseDto> CreateIncomingDocumentAsync(CreateIncomingDocumentDto dto);
        Task<DocumentResponseDto> CreateOutgoingDocumentAsync(CreateOutgoingDocumentDto dto);
        Task<DocumentResponseDto> CreateInternalDocumentAsync(CreateInternalDocumentDto dto);
        Task<DocumentResponseDto> UpdateDocumentAsync(Guid id, UpdateDocumentDto dto);
        Task<DocumentResponseDto> ChangeDocumentStatusAsync(Guid id, ChangeDocumentStatusDto dto);
        Task<DocumentResponseDto> AssignDepartmentAccessAsync(Guid id, AssignDepartmentAccessDto dto);
        Task<DocumentResponseDto> AddAttachmentAsync(Guid id, AddAttachmentDto dto);
        Task<DocumentResponseDto> RemoveAttachmentAsync(Guid id, Guid attachmentId);
        Task<DocumentResponseDto?> GetDocumentByIdAsync(Guid id);
        Task<PagedResultDto<DocumentResponseDto>> GetDocumentsAsync(DocumentFilterDto filter);
        Task<bool> DeleteDocumentAsync(Guid id);
    }
}
