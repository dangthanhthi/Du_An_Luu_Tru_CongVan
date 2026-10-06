using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        // Offline schema generation must not consume the application's database credentials.
        // Applying a reviewed migration requires an explicitly supplied --connection.
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=localhost;Database=DAS_Auth_DesignOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new AuthDbContext(options);
    }
}
