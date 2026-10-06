using FilesService.Models.Entities;
using FilesService.Models.DTOs;
namespace FilesService.Services;
public interface IFileStorageService
{
    Task<FileRecord> UploadFileAsync(IFormFile file, Guid userId, CancellationToken ct = default);
    Task<(Stream fileStream, string contentType, string fileName, string fileHash)> DownloadFileAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ManagedFileInfo> GetFileInfoAsync(Guid id, Guid userId, CancellationToken ct = default);
}
