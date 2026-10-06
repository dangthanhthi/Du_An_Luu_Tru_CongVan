using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocumentService;

public sealed class DocumentDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    public DocumentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseSqlServer("Server=localhost;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new DocumentDbContext(options);
    }
}
