using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

[ApiController]
[Route("api/auth/departments")]
[Authorize]
public class DepartmentsController(AuthDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(
        await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync()));
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(DepartmentRequest request)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code))
            return ApiResults.Error(this, StatusCodes.Status409Conflict, "Department code already exists.");
        var department = new Department { Name = request.Name.Trim(), Code = code };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Department>.Ok(department));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, DepartmentRequest request)
    {
        var department = await db.Departments.FindAsync(id);
        if (department is null) return ApiResults.Error(this, StatusCodes.Status404NotFound, "Department not found.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != id))
            return ApiResults.Error(this, StatusCodes.Status409Conflict, "Department code already exists.");
        department.Name = request.Name.Trim();
        department.Code = code;
        department.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<Department>.Ok(department));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var department = await db.Departments.FindAsync(id);
        if (department is null) return ApiResults.Error(this, StatusCodes.Status404NotFound, "Department not found.");
        department.IsActive = false;
        department.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { department.Id, department.IsActive }));
    }
}
