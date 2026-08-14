using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PartnerService;

[ApiController]
[Route("api/partners")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerBusinessService _partnerService;

    public PartnersController(IPartnerBusinessService partnerService)
    {
        _partnerService = partnerService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException(
                "Khong the xac dinh danh tinh nguoi dung tu token. Token co the khong hop le.");
        return userId;
    }

    private string? GetUserRole()
        => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirst("role")?.Value;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetList(
        [FromQuery] string? searchTerm,
        [FromQuery] string? entityType,
        [FromQuery] bool?   isActive,
        [FromQuery] int     pageNumber = 1,
        [FromQuery] int     pageSize   = 10)
    {
        var filter = new PartnerFilter(searchTerm, entityType, isActive, pageNumber, pageSize);
        var result = await _partnerService.GetListAsync(filter);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var partner = await _partnerService.GetByIdAsync(id);
        if (partner == null)
            return NotFound(new { success = false, message = $"Khong tim thay doi tac voi ID: {id}" });

        return Ok(new { success = true, data = partner });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePartnerRequest req)
    {
        var userId   = GetUserId();
        var userRole = GetUserRole();
        var partner  = await _partnerService.CreateAsync(req, userId, userRole);
        return CreatedAtAction(nameof(GetById), new { id = partner.Id },
            new { success = true, data = partner });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePartnerRequest req)
    {
        var userId   = GetUserId();
        var userRole = GetUserRole();
        var partner  = await _partnerService.UpdateAsync(id, req, userId, userRole);
        return Ok(new { success = true, data = partner });
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userRole = GetUserRole();
        var deleted  = await _partnerService.SoftDeleteAsync(id, userRole);
        if (!deleted)
            return NotFound(new { success = false, message = $"Khong tim thay doi tac voi ID: {id}" });

        return Ok(new { success = true, data = (object?)null });
    }

    [HttpPut("{id:guid}/restore")]
    [Authorize]
    public async Task<IActionResult> Restore(Guid id)
    {
        var userRole = GetUserRole();
        var partner  = await _partnerService.RestoreAsync(id, userRole);
        return Ok(new { success = true, data = partner });
    }
}
