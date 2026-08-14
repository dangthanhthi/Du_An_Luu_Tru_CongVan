using FilesService.Models.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FilesService.Services
{
    public interface IFileStorageService
    {
        // 1. Chỉ nhận 1 file và userId để lưu
        Task<FileRecord> UploadFileAsync(IFormFile file, Guid userId);

        // 2. Trả về stream vật lý kèm các metadata để tải file
        Task<(Stream fileStream, string contentType, string fileName, string fileHash)> DownloadFileAsync(Guid id);

        // 3. Chỉ lấy thông tin metadata (API Info)
        Task<FileRecord> GetFileInfoAsync(Guid id);
    }
}