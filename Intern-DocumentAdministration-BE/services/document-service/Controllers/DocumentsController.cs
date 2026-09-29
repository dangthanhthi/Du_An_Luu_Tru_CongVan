using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocumentService;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentBusinessService _documentService;

    public DocumentsController(IDocumentBusinessService documentService)
    {
        _documentService = documentService;
    }

    private DocumentActor Actor() => DocumentActor.FromPrincipal(User);

    /// <summary>
    /// Tạo mới Công văn đến (Incoming Document)
    /// </summary>
    [HttpPost("incoming")]
    public async Task<IActionResult> CreateIncoming([FromBody] CreateIncomingDocumentRequest req)
    {
        var doc = await _documentService.CreateIncomingAsync(req, Actor());
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tạo mới Công văn đi (Outgoing Document)
    /// </summary>
    [HttpPost("outgoing")]
    public async Task<IActionResult> CreateOutgoing([FromBody] CreateOutgoingDocumentRequest req)
    {
        var doc = await _documentService.CreateOutgoingAsync(req, Actor());
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tạo mới Công văn nội bộ (Internal Document)
    /// </summary>
    [HttpPost("internal")]
    public async Task<IActionResult> CreateInternal([FromBody] CreateInternalDocumentRequest req)
    {
        var doc = await _documentService.CreateInternalAsync(req, Actor());
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tìm kiếm, lọc và phân trang danh sách Công văn
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DocumentFilter filter)
    {
        var result = await _documentService.GetListAsync(filter, Actor());
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lấy chi tiết Công văn theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var doc = await _documentService.GetByIdAsync(id, Actor());
        if (doc == null)
        {
            return NotFound(new { success = false, message = $"Không tìm thấy công văn với ID: {id}" });
        }
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Cập nhật thông tin Công văn (chỉ khi trạng thái Draft)
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest req)
    {
        var doc = await _documentService.UpdateAsync(id, req, Actor());
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Chuyển trạng thái công văn (Draft -> Reviewed -> Distributed)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest req)
    {
        var doc = await _documentService.ChangeStatusAsync(id, req, Actor());
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Phân quyền phòng ban được truy cập công văn (DocumentDepartmentAccess)
    /// </summary>
    [HttpPut("{id:guid}/assign-departments")]
    public async Task<IActionResult> AssignDepartments(Guid id, [FromBody] AssignAccessRequest req)
    {
        var doc = await _documentService.AssignAccessAsync(id, req, Actor());
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Thêm file đính kèm vào công văn
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> AddAttachment(Guid id, [FromBody] AddAttachmentRequest req)
    {
        var doc = await _documentService.AddAttachmentAsync(id, req, Actor());
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Xóa file đính kèm khỏi công văn
    /// </summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId)
    {
        await _documentService.RemoveAttachmentAsync(id, attachmentId, Actor());
        return Ok(new { success = true, data = (object?)null });
    }

    /// <summary>
    /// Xóa Công văn
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _documentService.DeleteAsync(id, Actor());
        if (!deleted)
        {
            return NotFound(new { success = false, message = $"Không tìm thấy công văn với ID: {id}" });
        }
        return NoContent();
    }

    [HttpGet("access/files/{fileId:guid}")]
    public async Task<IActionResult> CanReadFile(Guid fileId)
    {
        var access = await _documentService.CanReadFileAsync(fileId, Actor());
        if (!access.HasValue) return NotFound();
        return access.Value ? NoContent() : Forbid();
    }

    [HttpGet("ingestion/exists")]
    [Authorize(Roles = "System")]
    public async Task<IActionResult> HasProcessedSourceMessage([FromQuery] string sourceMessageId)
    {
        if (string.IsNullOrWhiteSpace(sourceMessageId)) return BadRequest();
        return Ok(new
        {
            success = true,
            data = new { exists = await _documentService.HasProcessedSourceMessageAsync(sourceMessageId) }
        });
    }
}
