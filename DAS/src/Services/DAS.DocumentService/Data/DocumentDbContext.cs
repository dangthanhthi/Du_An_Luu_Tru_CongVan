using Microsoft.EntityFrameworkCore;
using DAS.DocumentService.Entities;

namespace DAS.DocumentService.Data
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
        {
        }

        public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
        public DbSet<DocumentAttachmentEntity> DocumentAttachments => Set<DocumentAttachmentEntity>();
        public DbSet<DocumentDepartmentAccessEntity> DocumentDepartmentAccesses => Set<DocumentDepartmentAccessEntity>();
        public DbSet<DocumentStatusHistoryEntity> DocumentStatusHistories => Set<DocumentStatusHistoryEntity>();
        public DbSet<DocumentNumberCounterEntity> DocumentNumberCounters => Set<DocumentNumberCounterEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Default Schema for DocumentService
            modelBuilder.HasDefaultSchema("document");

            // Map DocumentEntity -> document.documents
            modelBuilder.Entity<DocumentEntity>(entity =>
            {
                entity.ToTable("documents");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.DocumentNumber).IsUnique();

                entity.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(20).IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Draft");
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary");
                entity.Property(e => e.PartnerId).HasColumnName("partner_id");
                entity.Property(e => e.SenderDepartmentId).HasColumnName("sender_department_id");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
                entity.Property(e => e.ReceivedAt).HasColumnName("received_at");
                entity.Property(e => e.DistributedAt).HasColumnName("distributed_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => new { e.DocType, e.Status }).HasDatabaseName("IX_Documents_DocType_Status");
                entity.HasIndex(e => e.PartnerId).HasDatabaseName("IX_Documents_PartnerId");
            });

            // Map DocumentAttachmentEntity -> document.document_attachments
            modelBuilder.Entity<DocumentAttachmentEntity>(entity =>
            {
                entity.ToTable("document_attachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DocumentId).HasColumnName("document_id").IsRequired();
                entity.Property(e => e.FileId).HasColumnName("file_id").IsRequired();
                entity.Property(e => e.AttachmentType).HasColumnName("attachment_type").HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasOne(e => e.Document)
                      .WithMany(d => d.Attachments)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.DocumentId).HasDatabaseName("IX_DocumentAttachments_DocumentId");
            });

            // Map DocumentDepartmentAccessEntity -> document.document_department_accesses
            modelBuilder.Entity<DocumentDepartmentAccessEntity>(entity =>
            {
                entity.ToTable("document_department_accesses");
                entity.HasKey(e => new { e.DocumentId, e.DepartmentId });

                entity.Property(e => e.DocumentId).HasColumnName("document_id");
                entity.Property(e => e.DepartmentId).HasColumnName("department_id");
                entity.Property(e => e.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id").IsRequired();

                entity.HasOne(e => e.Document)
                      .WithMany(d => d.DepartmentAccesses)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.DepartmentId).HasDatabaseName("IX_DocumentDepartmentAccess_DepartmentId");
            });

            // Map DocumentStatusHistoryEntity -> document.document_status_histories
            modelBuilder.Entity<DocumentStatusHistoryEntity>(entity =>
            {
                entity.ToTable("document_status_histories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DocumentId).HasColumnName("document_id").IsRequired();
                entity.Property(e => e.OldStatus).HasColumnName("old_status").HasMaxLength(20);
                entity.Property(e => e.NewStatus).HasColumnName("new_status").HasMaxLength(20).IsRequired();
                entity.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
                entity.Property(e => e.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);

                entity.HasOne(e => e.Document)
                      .WithMany(d => d.StatusHistories)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.DocumentId).HasDatabaseName("IX_DocumentStatusHistory_DocumentId");
            });

            // Map DocumentNumberCounterEntity -> document.document_number_counters
            modelBuilder.Entity<DocumentNumberCounterEntity>(entity =>
            {
                entity.ToTable("document_number_counters");
                entity.HasKey(e => new { e.DocType, e.Year });

                entity.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(20);
                entity.Property(e => e.Year).HasColumnName("year");
                entity.Property(e => e.CurrentValue).HasColumnName("current_value").HasDefaultValue(0);
            });
        }
    }
}
