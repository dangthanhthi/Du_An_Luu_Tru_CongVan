using FilesService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FilesService.Controllers
{
    [Route("api/files")] // Chuẩn xác prefix theo contract[cite: 1]
    [ApiController]
    [Authorize] // Bắt buộc kiểm tra JWT Token[cite: 1]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;

        public FilesController(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        // 1. TẢI FILE LÊN (Chỉ nhận 1 file theo contract)
        [HttpPost("upload")]
        [RequestSizeLimit(500L * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file) 
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object)null,
                    message = "File không hợp lệ hoặc trống",
                    errors = new[] { "file: rỗng" }
                });
            }

            // Trích xuất UserId từ JWT Token để lưu vào database
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng", errors = Array.Empty<string>() });
            }

            // Gọi service xử lý (bạn cần sửa lại hàm này bên trong IFileStorageService để chỉ nhận 1 file và userId)
            var record = await _fileStorageService.UploadFileAsync(file, userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = record.Id,
                    originalName = record.OriginalName,
                    sizeBytes = record.SizeBytes,
                    contentType = record.ContentType
                },
                message = (string)null,
                errors = Array.Empty<string>()
            });
        }

        // 2. TẢI / STREAM FILE VẬT LÝ
        [HttpGet("{id}")] // Route chuẩn theo contract[cite: 1]
        public async Task<IActionResult> DownloadFile(Guid id)
        {
            var (fileStream, contentType, fileName, fileHash) = await _fileStorageService.DownloadFileAsync(id);

            if (fileStream == null)
            {
                return NotFound(new { success = false, data = (object)null, message = "Không tìm thấy file trên hệ thống", errors = Array.Empty<string>() });
            }

            var entityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{fileHash}\"");

            return File(
                fileStream: fileStream,
                contentType: contentType,
                lastModified: null,
                entityTag: entityTag,
                enableRangeProcessing: true // Vẫn giữ lại tính năng rất hay này của bạn
            );
        }

        // 3. LẤY THÔNG TIN METADATA FILE
        [HttpGet("{id}/info")] // Route chuẩn theo contract[cite: 1]
        public async Task<IActionResult> GetFileInfo(Guid id)
        {
            var record = await _fileStorageService.GetFileInfoAsync(id);

            if (record == null)
            {
                return NotFound(new { success = false, data = (object)null, message = "Không tìm thấy thông tin file", errors = Array.Empty<string>() });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = record.Id,
                    originalName = record.OriginalName,
                    sizeBytes = record.SizeBytes,
                    contentType = record.ContentType
                },
                message = (string)null,
                errors = Array.Empty<string>()
            });
        }
    }
}