using System;

namespace EmailWorkerService.Models;

public class EmailScanItemLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScanLogId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string SenderEmail { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string? AttachmentName { get; set; }

    public Guid? FileId { get; set; }

    public Guid? PartnerId { get; set; }

    public string? ExtractedReferenceNumber { get; set; }

    public string? ExtractedSubject { get; set; }

    public string? DocumentId { get; set; }

    public string Status { get; set; } = "Scanning";

    public string? ErrorMessage { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? IntakeConfirmedAt { get; set; }
}