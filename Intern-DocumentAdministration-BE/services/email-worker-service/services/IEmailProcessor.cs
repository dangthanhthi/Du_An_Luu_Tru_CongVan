using EmailWorkerService.Models;

namespace EmailWorkerService.Services;

public interface IEmailProcessor
{
    Task<EmailScanResult> ProcessIncomingEmailsAsync(
        string triggerType = "Scheduled",
        CancellationToken cancellationToken = default);
}
