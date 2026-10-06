using FilesService.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FilesService.Data
{
    public class FileDbContext : DbContext
    {
        public FileDbContext(DbContextOptions<FileDbContext> options) : base(options) { }

        public DbSet<FileRecord> Files { get; set; } //[cite: 4]

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Set mặc định schema là "files" theo đúng kiến trúc[cite: 4]
            modelBuilder.HasDefaultSchema("files");
            modelBuilder.Entity<FileRecord>().ToTable("Files");
            modelBuilder.Entity<PdfClaim>(e => {
                e.ToTable("PdfClaims", t => {
                    t.HasCheckConstraint("CK_PdfClaim_State", "[State] IN ('Prepared','Active','Retired','Deleted')");
                    t.HasCheckConstraint("CK_PdfClaim_Version", "[Version] >= 1 AND [ExpectedVersion] >= 1");
                });
                e.HasKey(x => x.OperationId); e.HasIndex(x => x.FileId).IsUnique();
                e.HasIndex(x => x.State); e.Property(x => x.State).HasMaxLength(16);
                e.Property(x => x.Version).IsConcurrencyToken();
                e.HasOne<PdfUpload>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<PdfUpload>(e => {
                e.ToTable("PdfUploads", t => {
                    t.HasCheckConstraint("CK_PdfUpload_State", "[State] IN ('Receiving','Available','PendingScan','Rejected','Failed','Missing')");
                    t.HasCheckConstraint("CK_PdfUpload_Version", "[Version] >= 1");
                    t.HasCheckConstraint("CK_PdfUpload_Size", "[SizeBytes] IS NULL OR [SizeBytes] BETWEEN 1 AND 26214400");
                });
                e.HasKey(x => x.FileId); e.Property(x => x.StorageKey).HasMaxLength(36); e.HasIndex(x => x.StorageKey).IsUnique();
                e.Property(x => x.OriginalName).HasMaxLength(200); e.Property(x => x.State).HasMaxLength(16);
                e.Property(x => x.FailureCode).HasMaxLength(64); e.Property(x => x.Sha256).HasMaxLength(64);
                e.Property(x => x.Version).IsConcurrencyToken(); e.HasIndex(x => new { x.State, x.CreatedAt }); e.HasIndex(x => x.UploaderUserId);
            });
        }
    }
}
