using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

        public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;
        public DbSet<InAppNotification> InAppNotifications { get; set; } = null!;
        public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("notification");

            // Tối ưu hóa hiệu năng truy vấn quả chuông thông báo
            modelBuilder.Entity<InAppNotification>()
                .ToTable("InAppNotifications")
                .HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt });

            modelBuilder.Entity<NotificationLog>()
                .ToTable("NotificationLogs")
                .HasIndex(l => l.SentAt);

            modelBuilder.Entity<UserNotificationPreference>()
                .ToTable("UserNotificationPreferences");
        }
    }
}