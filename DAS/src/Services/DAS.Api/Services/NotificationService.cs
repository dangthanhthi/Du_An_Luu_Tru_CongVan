using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAS.Api.Data;
using DAS.Api.Models;

namespace DAS.Api.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(Guid userId, string title, string message, string link, string type);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId);
        Task<bool> MarkAsReadAsync(Guid notiId);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Notification> CreateNotificationAsync(Guid userId, string title, string message, string link, string type)
        {
            var noti = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();
            return noti;
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(Guid notiId)
        {
            var noti = await _context.Notifications.FindAsync(notiId);
            if (noti == null) return false;

            noti.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
