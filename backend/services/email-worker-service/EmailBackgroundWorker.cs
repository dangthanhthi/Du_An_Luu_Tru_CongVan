using EmailWorkerService.Data;
using EmailWorkerService.Services;
using Microsoft.EntityFrameworkCore;

namespace EmailWorkerService;

public class EmailBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailBackgroundWorker> _logger;
    private readonly IConfiguration _configuration;

    public EmailBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<EmailBackgroundWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = 60;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmailProcessor>();
                await processor.ProcessIncomingEmailsAsync("Scheduled", stoppingToken);

                var db = scope.ServiceProvider.GetRequiredService<EmailWorkerDbContext>();
                var configuredInterval = await db.EmailImapSettings
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => (int?)x.AutoScanIntervalMinutes)
                    .FirstOrDefaultAsync(stoppingToken);

                intervalMinutes = configuredInterval
                                  ?? ParseIntervalFromConfiguration(_configuration)
                                  ?? 60;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled email scan failed.");
                intervalMinutes = ParseIntervalFromConfiguration(_configuration) ?? 60;
            }

            intervalMinutes = Math.Clamp(intervalMinutes, 1, 1440);
            _logger.LogInformation("Next scheduled email scan in {Minutes} minute(s).", intervalMinutes);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static int? ParseIntervalFromConfiguration(IConfiguration configuration)
    {
        var raw = configuration["ImapSettings:AutoScanIntervalMinutes"];
        return int.TryParse(raw, out var value) ? value : null;
    }
}
