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
        }
    }
}