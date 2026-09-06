using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Data;
using NotificationService.Hubs;
using NotificationService.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NotificationService.Services
{
    public class NotificationBackgroundWorker : BackgroundService
    {
        private readonly INotificationQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationBackgroundWorker> _logger;

        public NotificationBackgroundWorker(
            INotificationQueue queue,
            IServiceScopeFactory scopeFactory,
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationBackgroundWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationBackgroundWorker đã khởi động và sẵn sàng xử lý hàng đợi.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var task = await _queue.DequeueNotificationAsync(stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    // 1. Tạo In-App Notification nếu có RecipientUserId
                    if (task.RecipientUserId.HasValue && task.RecipientUserId.Value != Guid.Empty)
                    {
                        var inApp = new InAppNotification
                        {
                            Id = Guid.NewGuid(),
                            RecipientUserId = task.RecipientUserId.Value,
                            Title = task.Subject,
                            Message = task.Body,
                            ActionUrl = task.ActionUrl,
                            RelatedDocumentId = task.RelatedDocumentId,
                            NotificationType = task.NotificationType,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        db.InAppNotifications.Add(inApp);
                        await db.SaveChangesAsync(stoppingToken);

                        // 2. Real-time Push qua SignalR Hub
                        await _hubContext.Clients.Group($"user_{task.RecipientUserId.Value}")
                            .SendAsync("ReceiveNotification", new
                            {
                                id = inApp.Id,
                                title = inApp.Title,
                                message = inApp.Message,
                                actionUrl = inApp.ActionUrl,
                                type = inApp.NotificationType,
                                createdAt = inApp.CreatedAt,
                                isRead = false
                            }, cancellationToken: stoppingToken);
                    }

                    // 3. Kiểm tra tùy chọn người dùng (User Preferences) trước khi gửi Email
                    bool shouldSendEmail = true;
                    if (task.RecipientUserId.HasValue)
                    {
                        var pref = await db.UserNotificationPreferences
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.UserId == task.RecipientUserId.Value, stoppingToken);

                        if (pref != null)
                        {
                            if (!pref.EmailEnabled) shouldSendEmail = false;
                            if (pref.UrgentOnly && !string.Equals(task.NotificationType, "Urgent", StringComparison.OrdinalIgnoreCase))
                            {
                                shouldSendEmail = false;
                            }
                        }
                    }

                    // 4. Gửi Email với cơ chế Exponential Backoff Retry
                    bool isEmailSuccess = false;
                    string? emailError = null;

                    if (shouldSendEmail && !string.IsNullOrWhiteSpace(task.RecipientEmail))
                    {
                        var htmlBody = EmailTemplateHelper.BuildDocumentNotificationHtml(
                            task.Subject,
                            task.Body,
                            task.Subject,
                            task.ActionUrl
                        );

                        int maxRetries = 3;
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            var result = await emailService.SendEmailAsync(task.RecipientEmail, task.Subject, htmlBody);
                            if (result.IsSuccess)
                            {
                                isEmailSuccess = true;
                                emailError = null;
                                break;
                            }

                            emailError = result.ErrorMessage;
                            _logger.LogWarning("Gửi email tới {Email} thất bại (Lần thử {Attempt}/{Max}): {Error}", task.RecipientEmail, attempt, maxRetries, emailError);

                            if (attempt < maxRetries)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
                            }
                        }
                    }
                    else
                    {
                        isEmailSuccess = true; // Bỏ qua gửi email theo cấu hình người dùng
                    }

                    // 5. Ghi log lịch sử gửi
                    var log = new NotificationLog
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = task.RecipientUserId,
                        RecipientEmail = task.RecipientEmail,
                        Subject = task.Subject,
                        RelatedDocumentId = task.RelatedDocumentId,
                        Status = isEmailSuccess ? "Sent" : "Failed",
                        ErrorMessage = emailError,
                        SentAt = DateTime.UtcNow
                    };

                    db.NotificationLogs.Add(log);
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý Notification Background Worker.");
                }
            }
        }
    }
}
