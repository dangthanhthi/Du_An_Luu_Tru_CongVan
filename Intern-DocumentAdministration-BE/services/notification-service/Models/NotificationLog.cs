using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Models
{
    [Table("NotificationLogs", Schema = "notification")]
    public class NotificationLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid RecipientUserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string RecipientEmail { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Subject { get; set; } = null!;

        public Guid? RelatedDocumentId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Sent"; // Nhận 1 trong 2 giá trị: Sent hoặc Failed

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}