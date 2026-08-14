using System;
using System.Collections.Generic;

namespace DAS.DocumentService.Entities
{
    public enum DocumentType
    {
        INCOMING,
        OUTGOING,
        INTERNAL
    }

    public enum DocumentStatus
    {
        Draft,
        Reviewed,
        Distributed
    }

    public class DocumentEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty; // INCOMING, OUTGOING, INTERNAL
        public string Status { get; set; } = DocumentStatus.Draft.ToString();
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        
        // Logical reference to partner.external_entities
        public Guid? PartnerId { get; set; }

        // Logical reference to auth.departments (for Outgoing/Internal)
        public Guid? SenderDepartmentId { get; set; }

        // Logical reference to auth.users
        public Guid CreatedByUserId { get; set; }

        public DateTime? ReceivedAt { get; set; }
        public DateTime? DistributedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<DocumentAttachmentEntity> Attachments { get; set; } = new List<DocumentAttachmentEntity>();
        public ICollection<DocumentDepartmentAccessEntity> DepartmentAccesses { get; set; } = new List<DocumentDepartmentAccessEntity>();
        public ICollection<DocumentStatusHistoryEntity> StatusHistories { get; set; } = new List<DocumentStatusHistoryEntity>();
    }

    public class DocumentAttachmentEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public Guid FileId { get; set; } // Logical reference to files.files
        public string? AttachmentType { get; set; } // Original, Scan, Reference
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DocumentEntity? Document { get; set; }
    }

    public class DocumentDepartmentAccessEntity
    {
        public Guid DocumentId { get; set; }
        public Guid DepartmentId { get; set; } // Logical reference to auth.departments
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Guid AssignedByUserId { get; set; } // Logical reference to auth.users

        public DocumentEntity? Document { get; set; }
    }

    public class DocumentStatusHistoryEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public string? OldStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public Guid ChangedByUserId { get; set; } // Logical reference to auth.users
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }

        public DocumentEntity? Document { get; set; }
    }

    public class DocumentNumberCounterEntity
    {
        public string DocType { get; set; } = string.Empty; // INCOMING, OUTGOING, INTERNAL
        public int Year { get; set; }
        public int CurrentValue { get; set; } = 0;
    }
}
