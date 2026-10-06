using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Models
{
    [Table("UserNotificationPreferences", Schema = "notification")]
    public class UserNotificationPreference
    {
        [Key]
        public Guid UserId { get; set; }

        public bool EmailEnabled { get; set; } = true;

        public bool InAppEnabled { get; set; } = true;

        public bool UrgentOnly { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
