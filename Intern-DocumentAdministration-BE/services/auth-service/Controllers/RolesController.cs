using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

[ApiController]
[Route("api/auth/roles")]
[Authorize(Roles = "Admin")]
public class RolesController(AuthDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(
        await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name))
            return ApiResults.Error(this, StatusCodes.Status409Conflict, "Role name already exists.");
        var role = new Role { Name = name, Description = request.Description?.Trim() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Role>.Ok(role));
    }
}
