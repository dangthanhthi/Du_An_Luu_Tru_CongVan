using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DAS.DocumentService.DTOs;
using DAS.DocumentService.Services;

namespace DAS.DocumentService.Controllers
{
    [ApiController]
    [Route("api/v1/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        /// <summary>
        /// Tạo mới Công văn đến (Incoming Document)
        /// </summary>
        [HttpPost("incoming")]
        public async Task<IActionResult> CreateIncoming([FromBody] CreateIncomingDocumentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _documentService.CreateIncomingDocumentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Tạo mới Công văn đi (Outgoing Document)
        /// </summary>
        [HttpPost("outgoing")]
        public async Task<IActionResult> CreateOutgoing([FromBody] CreateOutgoingDocumentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _documentService.CreateOutgoingDocumentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Tạo mới Công văn nội bộ (Internal Document)
        /// </summary>
        [HttpPost("internal")]
        public async Task<IActionResult> CreateInternal([FromBody] CreateInternalDocumentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _documentService.CreateInternalDocumentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Tìm kiếm, lọc và phân trang danh sách Công văn
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] DocumentFilterDto filter)
        {
            var result = await _documentService.GetDocumentsAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết Công văn theo ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _documentService.GetDocumentByIdAsync(id);
            if (result == null) return NotFound(new { message = $"Không tìm thấy công văn với ID {id}" });
            return Ok(result);
        }

        /// <summary>
        /// Cập nhật thông tin Công văn
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _documentService.UpdateDocumentAsync(id, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Chuyển trạng thái công văn (Workflow: Draft -> Reviewed -> Distributed)
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeDocumentStatusDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _documentService.ChangeDocumentStatusAsync(id, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Phân quyền xem công văn theo Phòng ban (DocumentDepartmentAccess)
        /// </summary>
        [HttpPost("{id:guid}/access")]
        public async Task<IActionResult> AssignAccess(Guid id, [FromBody] AssignDepartmentAccessDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _documentService.AssignDepartmentAccessAsync(id, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Health check endpoint cho Docker & API Gateway
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "Healthy", service = "DAS.DocumentService", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Thêm file đính kèm vào công văn
        /// </summary>
        [HttpPost("{id:guid}/attachments")]
        public async Task<IActionResult> AddAttachment(Guid id, [FromBody] AddAttachmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _documentService.AddAttachmentAsync(id, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa file đính kèm khỏi công văn
        /// </summary>
        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId)
        {
            try
            {
                var result = await _documentService.RemoveAttachmentAsync(id, attachmentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa Công văn
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _documentService.DeleteDocumentAsync(id);
            if (!success) return NotFound(new { message = $"Không tìm thấy công văn với ID {id}" });
            return NoContent();
        }
    }
}
