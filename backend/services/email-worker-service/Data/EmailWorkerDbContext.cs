using EmailWorkerService.Models;
using Microsoft.EntityFrameworkCore;

namespace EmailWorkerService.Data;

public class EmailWorkerDbContext : DbContext
{
    public EmailWorkerDbContext(DbContextOptions<EmailWorkerDbContext> options) : base(options)
    {
    }

    public DbSet<EmailImapSettings> EmailImapSettings => Set<EmailImapSettings>();
    public DbSet<EmailScanLog> EmailScanLogs => Set<EmailScanLog>();
    public DbSet<EmailScanItemLog> EmailScanItemLogs => Set<EmailScanItemLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailImapSettings>(entity =>
        {
            entity.ToTable("EmailImapSettings", "emailworker");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ImapHost)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.EmailAddress)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.AppPassword)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.WhitelistedDomains)
                .HasMaxLength(1000)
                .IsRequired();
        });

        modelBuilder.Entity<EmailScanLog>(entity =>
        {
            entity.Property(x => x.CurrentEmailSubject)
    .HasMaxLength(1000);

            entity.Property(x => x.CurrentSenderEmail)
                .HasMaxLength(320);
            entity.ToTable("EmailScanLogs", "emailworker");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(x => x.TriggerType)
                .HasMaxLength(20)
                .IsRequired();
        });

        modelBuilder.Entity<EmailScanItemLog>(entity =>
        {
            entity.ToTable("EmailScanItemLogs", "emailworker");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.SenderEmail)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(x => x.Subject)
                .HasMaxLength(1000);

            entity.Property(x => x.AttachmentName)
                .HasMaxLength(500);

            entity.Property(x => x.ExtractedReferenceNumber)
                .HasMaxLength(200);

            entity.Property(x => x.ExtractedSubject)
                .HasMaxLength(1000);

            entity.Property(x => x.DocumentId)
                .HasMaxLength(100);

            entity.Property(x => x.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            entity.HasIndex(x => x.ScanLogId);

            entity.HasOne<EmailScanLog>()
                .WithMany()
                .HasForeignKey(x => x.ScanLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}