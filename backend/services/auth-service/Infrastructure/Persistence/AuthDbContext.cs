using Microsoft.EntityFrameworkCore;
using AuthService.Organization;

namespace AuthService;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DirectoryProjectionState> DirectoryProjections => Set<DirectoryProjectionState>();
    public DbSet<DirectoryInboxReceipt> DirectoryInbox => Set<DirectoryInboxReceipt>();
    public DbSet<DirectoryOutboxEvent> DirectoryOutbox => Set<DirectoryOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        modelBuilder.Entity<DirectoryProjectionState>(entity =>
        {
            entity.ToTable("DirectoryProjections");
            entity.HasKey(x => x.SourceId);
            entity.Property(x => x.SourceId).HasMaxLength(64);
            entity.Property(x => x.Fingerprint).HasMaxLength(64);
            entity.Property(x => x.AuthorizationRevision).IsConcurrencyToken();
            entity.Property(x => x.Payload).IsRequired();
        });
        modelBuilder.Entity<DirectoryInboxReceipt>(entity =>
        {
            entity.ToTable("DirectoryInbox");
            entity.HasKey(x => new { x.SourceId, x.MessageId });
            entity.Property(x => x.SourceId).HasMaxLength(64);
            entity.Property(x => x.MessageId).HasMaxLength(128);
            entity.Property(x => x.PayloadHash).HasMaxLength(64);
        });
        modelBuilder.Entity<DirectoryOutboxEvent>(entity =>
        {
            entity.ToTable("DirectoryOutbox");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceId).HasMaxLength(64);
            entity.Property(x => x.EventType).HasMaxLength(64);
            entity.HasIndex(x => new { x.SourceId, x.AuthorizationRevision }).IsUnique();
            entity.HasIndex(x => x.PublishedAt);
        });

        modelBuilder.Entity<UserRole>().HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        modelBuilder.Entity<UserRole>()
            .HasOne(userRole => userRole.User)
            .WithMany(user => user.UserRoles)
            .HasForeignKey(userRole => userRole.UserId);
        modelBuilder.Entity<UserRole>()
            .HasOne(userRole => userRole.Role)
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId);

        modelBuilder.Entity<User>()
            .HasOne(user => user.Department)
            .WithMany(department => department.Users)
            .HasForeignKey(user => user.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<RefreshToken>()
            .HasOne(refreshToken => refreshToken.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(role => role.Name).IsUnique();
        modelBuilder.Entity<Department>().HasIndex(department => department.Code).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(refreshToken => refreshToken.Token).IsUnique();
    }
}
