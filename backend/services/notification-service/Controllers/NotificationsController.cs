using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace NotificationService.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    [RequestSizeLimit(8192)]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly DurableNotifications _queue;

        public NotificationsController(NotificationDbContext context, DurableNotifications queue)
        {
            _context = context;
            _queue = queue;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            {
                throw new UnauthorizedAccessException("Authenticated identity is required.");
            }
            return userId;
        }

        /// <summary>
        /// Gửi thông báo bất đồng bộ qua Background Queue siêu tốc (1-2ms)
        /// </summary>
        [HttpPost("send")]
        [Authorize(Policy="NotificationSend")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmail) && !request.RecipientUserId.HasValue)
            {
                return BadRequest(new { success = false, message = "Cần cung cấp RecipientEmail hoặc RecipientUserId.", errors = new[] { "Missing recipient" } });
            }

            var keys=Request.Headers["Idempotency-Key"];
            if(keys.Count!=1)return BadRequest(new{code="INVALID_IDEMPOTENCY_KEY"});
            try {
                var accepted=await _queue.AcceptAsync(GetCurrentUserId(),keys[0]!,new(request.RecipientUserId,request.RecipientEmail,request.Subject,request.Body,request.RelatedDocumentId,request.NotificationType??"Info",request.ActionUrl,request.Cc),HttpContext.RequestAborted);
                return Accepted(new{success=true,data=new{accepted.Id,accepted.State}});
            }catch(NotificationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
        }

        /// <summary>
        /// Lấy danh sách thông báo quả chuông của tôi (In-App Notifications)
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool? unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if(page<1||page>1000000||pageSize<1||pageSize>100)return BadRequest();
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
                .ThenBy(n=>n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    // SQL datetime2 and SQLite do not preserve DateTime.Kind.
                    // These fields are written as UTC; keep that contract in JSON.
                    items = items.Select(n => new {
                        n.Id, n.RecipientUserId, n.Title, n.Message, n.ActionUrl,
                        n.RelatedDocumentId, n.NotificationType, n.IsRead,
                        ReadAt = n.ReadAt.HasValue ? DateTime.SpecifyKind(n.ReadAt.Value, DateTimeKind.Utc) : (DateTime?)null,
                        CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)
                    }),
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
        [Authorize(Policy="NotificationAudit")]
        public IActionResult GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if(page<1||page>1000000||pageSize<1||pageSize>100)return BadRequest();
            var logs = _context.Set<DeliveryInbox>().AsNoTracking()
                .OrderByDescending(x => x.CreatedAt).ThenBy(x=>x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x=>new{x.Id,x.State,x.Attempts,x.CreatedAt,x.Version})
                .ToList();

            return Ok(new { success = true, data = logs, message = (string?)null, errors = Array.Empty<string>() });
        }
    }

    public class SendNotificationRequest
    {
        public Guid? RecipientUserId { get; set; }
        public string? RecipientEmail { get; set; }
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
        public Guid? RelatedDocumentId { get; set; }
        public string? NotificationType { get; set; } // Info, Urgent, Success, Warning
        public string? ActionUrl { get; set; }
        public IReadOnlyList<string>? Cc {get;set;}
    }

    public class UpdatePreferenceRequest
    {
        public bool EmailEnabled { get; set; } = true;
        public bool InAppEnabled { get; set; } = true;
        public bool UrgentOnly { get; set; } = false;
    }
}
