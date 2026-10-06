namespace EmailWorkerService.Models;

public class EmailImapSettings
{
    public int Id { get; set; } = 1;
    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string EmailAddress { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
    public string WhitelistedDomains { get; set; } = string.Empty;
    public int AutoScanIntervalMinutes { get; set; } = 60;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record SaveImapSettingsRequest(
    string ImapHost,
    int ImapPort,
    bool UseSsl,
    string EmailAddress,
    string? AppPassword,
    string? WhitelistedDomains,
    int AutoScanIntervalMinutes);

public record TestImapConnectionRequest(
    string ImapHost,
    int ImapPort,
    bool UseSsl,
    string EmailAddress,
    string? AppPassword);

public record EmailScanResult(
    int EmailsScanned,
    int DocumentsCreated,
    bool Success,
    string? ErrorMessage);

// Legacy options model kept for compatibility with the old EmailWorker class.
// The active scanner uses EmailImapSettings from SQL Server.
public class EmailSettings
{
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string EmailAddress { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}
