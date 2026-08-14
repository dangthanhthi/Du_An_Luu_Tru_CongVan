using EmailWorkerService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmailWorkerService
{
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailBackgroundWorker> _logger;

        public EmailBackgroundWorker(IServiceProvider serviceProvider, ILogger<EmailBackgroundWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Background Worker khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Phải tạo Scope mới vì BackgroundService là Singleton, 
                    // trong khi EmailProcessor (có thể chứa HttpClient) thường là Scoped.
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var processor = scope.ServiceProvider.GetRequiredService<IEmailProcessor>();
                        await processor.ProcessIncomingEmailsAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình chạy tự động quét email.");
                }

                // Nghỉ 1 tiếng (3600000 mili-giây) rồi chạy lại[cite: 3]
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}