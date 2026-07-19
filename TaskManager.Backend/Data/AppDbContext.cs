using Microsoft.EntityFrameworkCore;
using TaskManager.Backend.Models;

namespace TaskManager.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
                entity.Property(u => u.Name).IsRequired().HasMaxLength(255);
                entity.Property(u => u.PasswordHash).HasMaxLength(255);
                entity.Property(u => u.ExternalId).HasMaxLength(255);
                entity.Property(u => u.Department).HasMaxLength(255);

                entity.Property(u => u.Role).HasConversion<string>();
                entity.Property(u => u.AuthProvider).HasConversion<string>();
                entity.Property(u => u.Status).HasConversion<string>();
            });
        }
    }
}
