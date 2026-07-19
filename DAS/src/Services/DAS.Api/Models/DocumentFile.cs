using System;

namespace DAS.Api.Models
{
    public class DocumentFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int Version { get; set; } = 1;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
