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

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException(
                "Không thể xác định danh tính người dùng từ token. " +
                "Vui lòng đăng nhập lại để lấy token hợp lệ.");
        return userId;
    }

    private string? GetUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirst("role")?.Value;
    }

    private Guid? GetUserDepartmentId()
    {
        var claim = User.FindFirstValue("departmentId");
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var deptId))
        {
            return deptId;
        }
        return null;
    }

    /// <summary>
    /// Tạo mới Công văn đến (Incoming Document)
    /// </summary>
    [HttpPost("incoming")]
    public async Task<IActionResult> CreateIncoming([FromBody] CreateIncomingDocumentRequest req)
    {
        var userId = GetUserId();
        var doc = await _documentService.CreateIncomingAsync(req, userId);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tạo mới Công văn đi (Outgoing Document)
    /// </summary>
    [HttpPost("outgoing")]
    public async Task<IActionResult> CreateOutgoing([FromBody] CreateOutgoingDocumentRequest req)
    {
        var userId = GetUserId();
        var doc = await _documentService.CreateOutgoingAsync(req, userId);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tạo mới Công văn nội bộ (Internal Document)
    /// </summary>
    [HttpPost("internal")]
    public async Task<IActionResult> CreateInternal([FromBody] CreateInternalDocumentRequest req)
    {
        var userId = GetUserId();
        var doc = await _documentService.CreateInternalAsync(req, userId);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { success = true, data = doc });
    }

    /// <summary>
    /// Tìm kiếm, lọc và phân trang danh sách Công văn
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DocumentFilter filter)
    {
        var userDeptId = GetUserDepartmentId();
        var userRole = GetUserRole();
        var result = await _documentService.GetListAsync(filter, userDeptId, userRole);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lấy chi tiết Công văn theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userDeptId = GetUserDepartmentId();
        var userRole = GetUserRole();
        var doc = await _documentService.GetByIdAsync(id, userDeptId, userRole);
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
        var userId = GetUserId();
        var userRole = GetUserRole();
        var doc = await _documentService.UpdateAsync(id, req, userId, userRole);
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Chuyển trạng thái công văn (Draft -> Reviewed -> Distributed)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest req)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        var doc = await _documentService.ChangeStatusAsync(id, req, userId, userRole);
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Phân quyền phòng ban được truy cập công văn (DocumentDepartmentAccess)
    /// </summary>
    [HttpPut("{id:guid}/assign-departments")]
    public async Task<IActionResult> AssignDepartments(Guid id, [FromBody] AssignAccessRequest req)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        var doc = await _documentService.AssignAccessAsync(id, req, userId, userRole);
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Thêm file đính kèm vào công văn
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> AddAttachment(Guid id, [FromBody] AddAttachmentRequest req)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        var doc = await _documentService.AddAttachmentAsync(id, req, userId, userRole);
        return Ok(new { success = true, data = doc });
    }

    /// <summary>
    /// Xóa file đính kèm khỏi công văn
    /// </summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        await _documentService.RemoveAttachmentAsync(id, attachmentId, userId, userRole);
        return Ok(new { success = true, data = (object?)null });
    }

    /// <summary>
    /// Xóa Công văn
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        var deleted = await _documentService.DeleteAsync(id, userId, userRole);
        if (!deleted)
        {
            return NotFound(new { success = false, message = $"Không tìm thấy công văn với ID: {id}" });
        }
        return Ok(new { success = true, data = (object?)null });
    }
}
