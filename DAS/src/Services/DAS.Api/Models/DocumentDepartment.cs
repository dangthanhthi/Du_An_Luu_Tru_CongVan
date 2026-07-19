using System;

namespace DAS.Api.Models
{
    public class DocumentDepartment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public Guid DepartmentId { get; set; }
        public string ProcessingStatus { get; set; } = "Chờ xử lý"; // Chờ xử lý, Đang xử lý, Đã hoàn thành
        public string Note { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}
