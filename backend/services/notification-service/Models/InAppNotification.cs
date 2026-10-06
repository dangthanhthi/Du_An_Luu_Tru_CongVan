using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Models
{
    [Table("InAppNotifications", Schema = "notification")]
    public class InAppNotification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid RecipientUserId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = null!;

        [MaxLength(500)]
        public string? ActionUrl { get; set; }

        public Guid? RelatedDocumentId { get; set; }

        [MaxLength(50)]
        public string NotificationType { get; set; } = "Info"; // Info, Urgent, Success, Warning

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
