using System;

namespace EmailWorkerService.Models;

public class EmailScanLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAt { get; set; }

    public int EmailsScanned { get; set; }

    public int TotalEmails { get; set; }

    public int DocumentsCreated { get; set; }

    public int ReadyForIntakeCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public string? CurrentEmailSubject { get; set; }

    public string? CurrentSenderEmail { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string TriggerType { get; set; } = "Scheduled";
}