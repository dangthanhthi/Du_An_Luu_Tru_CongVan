using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace DocumentService;

public static class DocumentTypeConstants
{
    public const string INCOMING = "INCOMING";
    public const string OUTGOING = "OUTGOING";
    public const string INTERNAL = "INTERNAL";
}

public static class DocumentStatusConstants
{
    public const string Draft = "Draft";
    public const string Reviewed = "Reviewed";
    public const string Distributed = "Distributed";
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentNumber { get; set; } = default!; // Số đến / số công văn nội bộ (CV-DEN-2026-0001)
    public string? ReferenceNumber { get; set; } // Số ký hiệu / công văn của đối tác ban hành (Reference No.)
    public string DocType { get; set; } = default!; // INCOMING / OUTGOING / INTERNAL
    public string Status { get; set; } = DocumentStatusConstants.Draft; // Draft / Reviewed / Distributed
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? SenderDepartmentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? DistributedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public List<DocumentAttachment> Attachments { get; set; } = new();
    public List<DocumentDepartmentAccess> DepartmentAccesses { get; set; } = new();
    public List<DocumentStatusHistory> StatusHistories { get; set; } = new();
}

public class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid FileId { get; set; }
    public string? AttachmentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Document? Document { get; set; }
}

public class DocumentDepartmentAccess
{
    public Guid DocumentId { get; set; }
    public Guid DepartmentId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid AssignedByUserId { get; set; }

    [JsonIgnore]
    public Document? Document { get; set; }
}

public class DocumentStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = default!;
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    [JsonIgnore]
    public Document? Document { get; set; }
}

public class DocumentNumberCounter
{
    public string DocType { get; set; } = default!;
    public int Year { get; set; }
    public int CurrentValue { get; set; } = 0;
}

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

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.ReferenceNumber);

        // Soft delete: tự động lọc bỏ công văn đã xóa khỏi mọi truy vấn
        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => !d.IsDeleted);

        modelBuilder.Entity<Document>()
            .HasIndex(d => new { d.DocType, d.Status });

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.PartnerId);

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
