using FilesService.Data;
using FilesService.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FilesService.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly FileDbContext _dbContext;
        private readonly string _storagePath;

        public FileStorageService(FileDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            // Đọc cấu hình Storage__Path từ biến môi trường (mặc định "/app/storage")
            _storagePath = configuration["Storage:Path"] ?? "/app/storage";

            // Tự động tạo thư mục nếu chưa tồn tại trên ổ cứng
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<FileRecord> UploadFileAsync(IFormFile file, Guid userId)
        {
            // 1. Tạo tên file vật lý ngẫu nhiên để không bị ghi đè nếu trùng tên file
            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var physicalPath = Path.Combine(_storagePath, uniqueFileName);

            // 2. Ghi file dạng stream xuống ổ cứng
            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 3. Tạo record để lưu xuống Database (Khớp với Schema files.Files)
            var record = new FileRecord
            {
                Id = Guid.NewGuid(),
                OriginalName = file.FileName,
                StoragePath = physicalPath,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                UploadedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Files.Add(record);
            await _dbContext.SaveChangesAsync();

            return record;
        }

        public async Task<(Stream fileStream, string contentType, string fileName, string fileHash)> DownloadFileAsync(Guid id)
        {
            // Tìm trong DB trước
            var record = await _dbContext.Files.FindAsync(id);
            if (record == null || !File.Exists(record.StoragePath))
            {
                return (null, null, null, null);
            }

            // Mở luồng đọc file vật lý (FileShare.Read giúp nhiều người tải cùng lúc không bị lock file)
            var stream = new FileStream(record.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Dùng chính ID làm Hash Etag giả lập để tận dụng cache trình duyệt
            var fileHash = record.Id.ToString();

            return (stream, record.ContentType, record.OriginalName, fileHash);
        }

        public async Task<FileRecord> GetFileInfoAsync(Guid id)
        {
            return await _dbContext.Files.FindAsync(id);
        }
    }
}