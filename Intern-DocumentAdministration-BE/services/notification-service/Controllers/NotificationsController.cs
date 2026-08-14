using Microsoft.AspNetCore.Mvc;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NotificationService.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly IEmailService _emailService;

        public NotificationsController(NotificationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            // 1. Thực thi gửi email
            var (isSuccess, errorMessage) = await _emailService.SendEmailAsync(request.RecipientEmail, request.Subject, request.Body);

            // 2. Ghi log vào Database
            var log = new NotificationLog
            {
                RecipientUserId = request.RecipientUserId,
                RecipientEmail = request.RecipientEmail,
                Subject = request.Subject,
                RelatedDocumentId = request.RelatedDocumentId,
                Status = isSuccess ? "Sent" : "Failed",
                ErrorMessage = errorMessage
            };

            _context.NotificationLogs.Add(log);
            await _context.SaveChangesAsync();

            // 3. Trả về Response chuẩn Form
            if (!isSuccess)
            {
                return StatusCode(500, new { success = false, data = (object?)null, message = "Gửi email thất bại", errors = new[] { errorMessage } });
            }

            return Ok(new { success = true, data = new { id = log.Id }, message = (string?)null, errors = Array.Empty<string>() });
        }

        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] int page = 1)
        {
            var pageSize = 20;
            var logs = _context.NotificationLogs
                .OrderByDescending(x => x.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new { success = true, data = logs, message = (string?)null, errors = Array.Empty<string>() });
        }
    }

    public class SendNotificationRequest
    {
        public Guid RecipientUserId { get; set; }
        public string RecipientEmail { get; set; } = null!; // Bổ sung để biết gửi đi đâu
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
        public Guid? RelatedDocumentId { get; set; }
    }
}