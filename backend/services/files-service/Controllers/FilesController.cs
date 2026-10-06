using System.Security.Claims;
using FilesService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
namespace FilesService.Controllers;

[Route("api/files")]
[ApiController]
[Authorize]
public sealed class FilesController(IFileStorageService storage) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 26L * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file) => await Execute(async actor => {
        if (!Request.HasFormContentType || (await Request.ReadFormAsync(HttpContext.RequestAborted)).Files.Count != 1)
            throw new FileRuleException(400,"ONE_FILE_REQUIRED","Upload exactly one PDF file.");
        var saved = await storage.UploadFileAsync(file, actor, HttpContext.RequestAborted);
        var info = await storage.GetFileInfoAsync(saved.Id, actor, HttpContext.RequestAborted);
        return StatusCode(info.State == "PendingScan" ? 202 : 200, Success(info));
    });
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> DownloadFile(Guid id) => await Execute(async actor => {
        var (stream, type, name, hash) = await storage.DownloadFileAsync(id, actor, HttpContext.RequestAborted);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = name }.ToString();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, type, lastModified: null, entityTag: new EntityTagHeaderValue('"' + hash + '"'), enableRangeProcessing: true);
    });
    [HttpGet("{id:guid}/info")]
    public async Task<IActionResult> GetFileInfo(Guid id) => await Execute(async actor => Ok(Success(await storage.GetFileInfoAsync(id, actor, HttpContext.RequestAborted))));
    private async Task<IActionResult> Execute(Func<Guid, Task<IActionResult>> action)
    {
        Response.Headers.CacheControl = "no-store";
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (User.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var actor) || actor == Guid.Empty)
            return Unauthorized(Failure("ACTOR_REQUIRED", "A valid authenticated uploader is required."));
        try { return await action(actor); }
        catch (FileRuleException e) { return StatusCode(e.Status, Failure(e.Code, e.Message)); }
        catch (Exception e) when (e is IOException or DbUpdateException or InvalidOperationException) {
            return StatusCode(503, Failure("FILE_SERVICE_UNAVAILABLE", "File storage is temporarily unavailable."));
        }
    }
    private static object Success(object data) => new { success = true, data, message = (string?)null, errors = Array.Empty<string>() };
    private static object Failure(string code, string message) => new { success = false, data = (object?)null, message, errors = new[] { code } };
}
