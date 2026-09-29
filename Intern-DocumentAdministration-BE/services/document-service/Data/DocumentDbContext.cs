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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("document");

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
