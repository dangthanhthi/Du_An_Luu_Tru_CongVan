using System;

namespace DAS.Api.Models
{
    public enum DocumentType
    {
        Incoming,
        Outgoing,
        Internal
    }

    public enum DocumentStatus
    {
        Draft,
        PendingReview,
        Approved,
        Distributed,
        Processing,
        Processed
    }

    public enum DocumentPriority
    {
        Normal,
        Urgent,
        Secret,
        TopSecret
    }

    public class Document
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocumentNumber { get; set; } = string.Empty;
        public DocumentType Type { get; set; } = DocumentType.Incoming;
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
        public DocumentPriority Priority { get; set; } = DocumentPriority.Normal;
        public Guid? ExternalEntityId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string? OriginalDocNumber { get; set; }
        public DateTime DocumentDate { get; set; } = DateTime.UtcNow;
        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
