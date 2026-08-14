using System;

namespace FilesService.Models.Entities
{
    public class FileRecord
    {
        public Guid Id { get; set; }
        public string OriginalName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public Guid UploadedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}