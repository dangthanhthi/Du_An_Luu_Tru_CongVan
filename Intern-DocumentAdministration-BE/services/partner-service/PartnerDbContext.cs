using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PartnerService;

public static class PartnerEntityTypeConstants
{
    public const string Sender    = "Sender";
    public const string Recipient = "Recipient";
    public const string Both      = "Both";

    public static readonly string[] AllValues = [Sender, Recipient, Both];
}

public class Partner
{
    public Guid     Id              { get; set; } = Guid.NewGuid();
    public string   FullName        { get; set; } = default!;
    public string   ShortName       { get; set; } = default!;
    public string   EntityType      { get; set; } = PartnerEntityTypeConstants.Both;
    public string?  Email           { get; set; }
    public string?  Phone           { get; set; }
    public string?  Address         { get; set; }
    public string?  TaxCode         { get; set; }
    public bool     IsActive        { get; set; } = true;
    public bool     IsDeleted       { get; set; } = false;
    public DateTime? DeletedAt      { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt      { get; set; }
    public Guid     CreatedByUserId { get; set; }
}

public class PartnerDbContext : DbContext
{
    public PartnerDbContext(DbContextOptions<PartnerDbContext> options) : base(options) { }

    public DbSet<Partner> Partners => Set<Partner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("partner");

        modelBuilder.Entity<Partner>()
            .HasIndex(p => p.ShortName).IsUnique();

        modelBuilder.Entity<Partner>()
            .HasIndex(p => p.TaxCode).IsUnique()
            .HasFilter("[TaxCode] IS NOT NULL");

        modelBuilder.Entity<Partner>()
            .HasQueryFilter(p => !p.IsDeleted);

        modelBuilder.Entity<Partner>()
            .HasIndex(p => p.EntityType);

        modelBuilder.Entity<Partner>()
            .HasIndex(p => new { p.IsDeleted, p.IsActive });
    }
}

public class PartnerDbContextFactory : IDesignTimeDbContextFactory<PartnerDbContext>
{
    public PartnerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PartnerDbContext>();
        optionsBuilder.UseSqlServer("Server=sqlserver;Database=DocumentManagementDb;User Id=sa;Password=DummyPassword;TrustServerCertificate=True;");
        return new PartnerDbContext(optionsBuilder.Options);
    }
}
