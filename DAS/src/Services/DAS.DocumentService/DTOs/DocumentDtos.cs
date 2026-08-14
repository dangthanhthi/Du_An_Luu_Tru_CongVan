using System;
using System.Collections.Generic;

namespace DAS.DocumentService.DTOs
{
    public class CreateIncomingDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public Guid? PartnerId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public List<Guid>? AttachmentFileIds { get; set; }
    }

    public class CreateOutgoingDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public Guid PartnerId { get; set; }
        public Guid SenderDepartmentId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public List<Guid>? AttachmentFileIds { get; set; }
    }

    public class CreateInternalDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public Guid SenderDepartmentId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public List<Guid>? AttachmentFileIds { get; set; }
    }

    public class UpdateDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public Guid? PartnerId { get; set; }
        public Guid? SenderDepartmentId { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }

    public class ChangeDocumentStatusDto
    {
        public string NewStatus { get; set; } = string.Empty; // Draft -> Reviewed -> Distributed
        public Guid ChangedByUserId { get; set; }
        public string? Note { get; set; }
    }

    public class AssignDepartmentAccessDto
    {
        public List<Guid> DepartmentIds { get; set; } = new List<Guid>();
        public Guid AssignedByUserId { get; set; }
    }

    public class DocumentAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid FileId { get; set; }
        public string? AttachmentType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddAttachmentDto
    {
        public Guid FileId { get; set; }
        public string? AttachmentType { get; set; } // Original, Scan, Reference
    }

    public class DocumentDepartmentAccessDto
    {
        public Guid DepartmentId { get; set; }
        public DateTime AssignedAt { get; set; }
        public Guid AssignedByUserId { get; set; }
    }

    public class DocumentStatusHistoryDto
    {
        public Guid Id { get; set; }
        public string? OldStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public Guid ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? Note { get; set; }
    }

    public class DocumentResponseDto
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public Guid? PartnerId { get; set; }
        public Guid? SenderDepartmentId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DistributedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<DocumentAttachmentDto> Attachments { get; set; } = new List<DocumentAttachmentDto>();
        public List<DocumentDepartmentAccessDto> DepartmentAccesses { get; set; } = new List<DocumentDepartmentAccessDto>();
        public List<DocumentStatusHistoryDto> StatusHistories { get; set; } = new List<DocumentStatusHistoryDto>();
    }

    public class DocumentFilterDto
    {
        public string? SearchTerm { get; set; }
        public string? DocType { get; set; } // INCOMING / OUTGOING / INTERNAL
        public string? Status { get; set; }
        public Guid? PartnerId { get; set; }
        public Guid? DepartmentId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    }
}
