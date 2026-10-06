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
            modelBuilder.Entity<DeliveryInbox>(e=>{
                e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.SenderId,x.KeyHash}).IsUnique();
                e.Property(x=>x.KeyHash).HasMaxLength(64);e.Property(x=>x.BodyHash).HasMaxLength(64);e.Property(x=>x.State).HasMaxLength(32);e.Property(x=>x.Version).IsConcurrencyToken();
                e.HasIndex(x=>new{x.State,x.NextAttemptUnix});
            });

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
