using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmailWorkerService.Data;

public sealed class DesignTimeEmailWorkerDbContextFactory : IDesignTimeDbContextFactory<EmailWorkerDbContext>
{
    // Offline migration commands do not start the host or read runtime credentials.
    public EmailWorkerDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<EmailWorkerDbContext>()
            .UseSqlServer("Server=localhost;Database=DAS_Email_DesignOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options);
}
