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
    public string?  ShortName       { get; set; }
    public string?  NormalizedShortName { get; set; }
    public string?  NormalizedTaxCode { get; set; }
    public string?  ContactPerson   { get; set; }
    public string?  ContactInformation { get; set; }
    public long     Version         { get; set; } = 1;
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
    public DbSet<PartnerAudit> PartnerAudits => Set<PartnerAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("partner");

        var partner = modelBuilder.Entity<Partner>();
        partner.Property(p => p.ShortName).HasMaxLength(450);
        partner.Property(p => p.TaxCode).HasMaxLength(450);
        partner.Property(p => p.NormalizedShortName).HasMaxLength(450);
        partner.Property(p => p.NormalizedTaxCode).HasMaxLength(450);
        partner.Property(p => p.Version).IsConcurrencyToken().HasDefaultValue(1L);
        partner.HasIndex(p => p.NormalizedShortName).IsUnique().HasFilter("[NormalizedShortName] IS NOT NULL");
        partner.HasIndex(p => p.NormalizedTaxCode).IsUnique().HasFilter("[NormalizedTaxCode] IS NOT NULL");
        var audit = modelBuilder.Entity<PartnerAudit>();
        audit.Property(a => a.Action).HasMaxLength(20);
        audit.HasIndex(a => new { a.PartnerId, a.Version }).IsUnique();
        audit.HasOne<Partner>().WithMany().HasForeignKey(a => a.PartnerId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Partner>()
            .HasQueryFilter(p => !p.IsDeleted);

        modelBuilder.Entity<Partner>()
            .HasIndex(p => p.EntityType);

        modelBuilder.Entity<Partner>()
            .HasIndex(p => new { p.IsDeleted, p.IsActive });
    }
}

public sealed class PartnerAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartnerId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
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
