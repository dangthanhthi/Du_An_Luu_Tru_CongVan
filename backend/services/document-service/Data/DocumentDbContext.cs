using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<DocumentDepartmentAccess> DocumentDepartmentAccess => Set<DocumentDepartmentAccess>();
    public DbSet<DocumentStatusHistory> DocumentStatusHistory => Set<DocumentStatusHistory>();
    public DbSet<DocumentNumberCounter> DocumentNumberCounters => Set<DocumentNumberCounter>();
    public DbSet<BusinessCatalogEntry> BusinessCatalogEntries => Set<BusinessCatalogEntry>();
    public DbSet<DistributionTarget> DistributionTargets => Set<DistributionTarget>();
    public DbSet<CatalogAuditEvent> CatalogAuditEvents => Set<CatalogAuditEvent>();
    public DbSet<DocumentRegistration> DocumentRegistrations => Set<DocumentRegistration>();
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<DocumentOutboxEvent> DocumentOutboxEvents => Set<DocumentOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("document");
        DocumentV2Model.Configure(modelBuilder, Database.IsSqlServer());
        modelBuilder.Entity<ReminderFanoutManifest>(e=>{e.HasKey(x=>x.BatchId);e.Property(x=>x.PlanHash).HasMaxLength(64);e.HasOne<ReminderBatch>().WithMany().HasForeignKey(x=>x.BatchId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<ReminderDelivery>(e=>{e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.BatchId,x.InputterUserId}).IsUnique();e.HasIndex(x=>new{x.State,x.NextAttemptUnix});e.Property(x=>x.State).HasMaxLength(32);e.Property(x=>x.NotificationState).HasMaxLength(32);e.Property(x=>x.PayloadJson).HasMaxLength(8000);e.Property(x=>x.Version).IsConcurrencyToken();e.HasOne<ReminderFanoutManifest>().WithMany().HasForeignKey(x=>x.BatchId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<ReminderBatch>(e=>{
            e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.DepartmentId,x.Period}).IsUnique();
            e.Property(x=>x.State).HasMaxLength(32);e.Property(x=>x.ErrorCode).HasMaxLength(100);
            e.Property(x=>x.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<DocumentTaskIntent>(e=>{
            e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.ActorId,x.KeyHash}).IsUnique();
            e.Property(x=>x.KeyHash).HasMaxLength(64);e.Property(x=>x.BodyHash).HasMaxLength(64);e.Property(x=>x.Title).HasMaxLength(250);e.Property(x=>x.State).HasMaxLength(32);e.Property(x=>x.RemoteTaskId).HasMaxLength(200);e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DocumentNotificationDelivery>(e=>{
            e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.EventId,x.RecipientId}).IsUnique();
            e.Property(x=>x.State).HasMaxLength(32);e.Property(x=>x.PayloadJson).HasMaxLength(8000);e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasIndex(x=>new{x.State,x.NextAttemptUnix});e.HasOne<DocumentOutboxEvent>().WithMany().HasForeignKey(x=>x.EventId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BusinessCatalogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Group).HasMaxLength(32);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.Group, x.Code }).IsUnique();
            e.HasData(BusinessCatalogSeed.Entries());
        });
        modelBuilder.Entity<DistributionTarget>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LegacyId).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Initial).HasMaxLength(32);
            e.Property(x => x.MappingState).HasMaxLength(16);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasData(BusinessCatalogSeed.Targets());
        });
        modelBuilder.Entity<CatalogAuditEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(32);
            e.HasIndex(x => x.EntryId);
        });

        modelBuilder.Entity<DocumentDepartmentAccess>()
            .ToTable("DocumentDepartmentAccess");
            
        modelBuilder.Entity<DocumentStatusHistory>()
            .ToTable("DocumentStatusHistory");

        modelBuilder.Entity<DocumentNumberCounter>()
            .HasKey(c => new { c.DocType, c.Year });

        modelBuilder.Entity<DocumentDepartmentAccess>()
            .HasKey(a => new { a.DocumentId, a.DepartmentId });

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.DocumentNumber).IsUnique();

        // Status transitions are compare-and-set operations. Including the status
        // originally loaded in EF's UPDATE predicate prevents two requests from
        // advancing the same state machine concurrently.
        modelBuilder.Entity<Document>()
            .Property(d => d.Status)
            .IsConcurrencyToken();

        // Soft delete: tự động lọc bỏ công văn đã xóa khỏi mọi truy vấn
        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => !d.IsDeleted);

        modelBuilder.Entity<Document>()
            .HasIndex(d => new { d.DocType, d.Status });

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.PartnerId);

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.SourceMessageId)
            .IsUnique()
            .HasFilter("[SourceMessageId] IS NOT NULL");

        modelBuilder.Entity<DocumentAttachment>()
            .HasOne(a => a.Document)
            .WithMany(d => d.Attachments)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentDepartmentAccess>()
            .HasOne(a => a.Document)
            .WithMany(d => d.DepartmentAccesses)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentStatusHistory>()
            .HasOne(h => h.Document)
            .WithMany(d => d.StatusHistories)
            .HasForeignKey(h => h.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
