using Microsoft.EntityFrameworkCore;

namespace AuthService;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

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
