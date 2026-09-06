using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NotificationService.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly INotificationQueue _queue;

        public NotificationsController(NotificationDbContext context, INotificationQueue queue)
        {
            _context = context;
            _queue = queue;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            {
                // Fallback test user ID for unauthenticated local testing
                return Guid.Parse("11111111-1111-1111-1111-111111111111");
            }
            return userId;
        }

        /// <summary>
        /// Gửi thông báo bất đồng bộ qua Background Queue siêu tốc (1-2ms)
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmail) && !request.RecipientUserId.HasValue)
            {
                return BadRequest(new { success = false, message = "Cần cung cấp RecipientEmail hoặc RecipientUserId.", errors = new[] { "Missing recipient" } });
            }

            var task = new NotificationTask(
                RecipientUserId: request.RecipientUserId,
                RecipientEmail: request.RecipientEmail,
                Subject: request.Subject,
                Body: request.Body,
                RelatedDocumentId: request.RelatedDocumentId,
                NotificationType: request.NotificationType ?? "Info",
                ActionUrl: request.ActionUrl
            );

            // Đẩy vào hàng đợi ngầm RAM, không chặn luồng request
            await _queue.QueueNotificationAsync(task);

            return Ok(new { success = true, message = "Thông báo đã được tiếp nhận và đưa vào hàng đợi xử lý ngầm.", errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Lấy danh sách thông báo quả chuông của tôi (In-App Notifications)
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool? unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            var query = _context.InAppNotifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId);

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                },
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// Lấy số lượng thông báo chưa đọc hiển thị số đỏ trên quả chuông (< 0.5ms)
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            var count = await _context.InAppNotifications
                .AsNoTracking()
                .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);

            return Ok(new { success = true, data = new { unreadCount = count }, errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Đánh dấu 1 thông báo là đã đọc
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = GetCurrentUserId();
            var notif = await _context.InAppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId);

            if (notif == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy thông báo.", errors = new[] { "Notification not found" } });
            }

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                notif.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Đã đánh dấu đã đọc.", errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo là đã đọc
        /// </summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            var now = DateTime.UtcNow;

            await _context.InAppNotifications
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));

            return Ok(new { success = true, message = "Đã đánh dấu tất cả thông báo là đã đọc.", errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Xóa bỏ 1 thông báo quả chuông
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(Guid id)
        {
            var userId = GetCurrentUserId();
            var notif = await _context.InAppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId);

            if (notif == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy thông báo.", errors = new[] { "Notification not found" } });
            }

            _context.InAppNotifications.Remove(notif);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa thông báo.", errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Lấy tùy chọn thông báo của người dùng
        /// </summary>
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var userId = GetCurrentUserId();
            var pref = await _context.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref == null)
            {
                pref = new UserNotificationPreference { UserId = userId };
            }

            return Ok(new { success = true, data = pref, errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Cập nhật tùy chọn thông báo của người dùng
        /// </summary>
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferenceRequest req)
        {
            var userId = GetCurrentUserId();
            var pref = await _context.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref == null)
            {
                pref = new UserNotificationPreference
                {
                    UserId = userId,
                    EmailEnabled = req.EmailEnabled,
                    InAppEnabled = req.InAppEnabled,
                    UrgentOnly = req.UrgentOnly,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserNotificationPreferences.Add(pref);
            }
            else
            {
                pref.EmailEnabled = req.EmailEnabled;
                pref.InAppEnabled = req.InAppEnabled;
                pref.UrgentOnly = req.UrgentOnly;
                pref.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = pref, errors = Array.Empty<string>() });
        }

        /// <summary>
        /// Lấy nhật ký gửi thông báo (Admin Logs)
        /// </summary>
        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
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
        public Guid? RecipientUserId { get; set; }
        public string RecipientEmail { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
        public Guid? RelatedDocumentId { get; set; }
        public string? NotificationType { get; set; } // Info, Urgent, Success, Warning
        public string? ActionUrl { get; set; }
    }

    public class UpdatePreferenceRequest
    {
        public bool EmailEnabled { get; set; } = true;
        public bool InAppEnabled { get; set; } = true;
        public bool UrgentOnly { get; set; } = false;
    }
}