using Microsoft.EntityFrameworkCore;
using DAS.Api.Models;

namespace DAS.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<ExternalEntity> ExternalEntities => Set<ExternalEntity>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
        public DbSet<DocumentDepartment> DocumentDepartments => Set<DocumentDepartment>();
        public DbSet<DocumentComment> DocumentComments => Set<DocumentComment>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>();
            modelBuilder.Entity<User>().Property(u => u.Status).HasConversion<string>();

            modelBuilder.Entity<Department>().HasIndex(d => d.Code).IsUnique();

            modelBuilder.Entity<ExternalEntity>().Property(e => e.Type).HasConversion<string>();

            modelBuilder.Entity<Document>().HasIndex(d => d.DocumentNumber).IsUnique();
            modelBuilder.Entity<Document>().Property(d => d.Type).HasConversion<string>();
            modelBuilder.Entity<Document>().Property(d => d.Status).HasConversion<string>();
            modelBuilder.Entity<Document>().Property(d => d.Priority).HasConversion<string>();
        }
    }
}
