using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

[ApiController]
[Route("api/auth/users")]
[Authorize(Roles = "Admin")]
public class UsersController(AuthDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? departmentId = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().Include(u => u.Department)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsQueryable();
        if (departmentId.HasValue)
            query = query.Where(u => u.DepartmentId == departmentId);

        var totalCount = await query.CountAsync();
        var users = await query.OrderBy(u => u.Username).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new PagedResult<object>(users.Select(ToResponse).ToList(), page, pageSize, totalCount)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await FindUser(id, true);
        return user is null
            ? ApiResults.Error(this, StatusCodes.Status404NotFound, "User not found.")
            : Ok(ApiResponse<object>.Ok(ToResponse(user)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username))
            return ApiResults.Error(this, StatusCodes.Status409Conflict, "Username already exists.", "username: must be unique");
        var validation = await ValidateReferences(request.DepartmentId, request.RoleIds);
        if (validation is not null) return validation;

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            DepartmentId = request.DepartmentId,
            UserRoles = request.RoleIds.Distinct().Select(roleId => new UserRole { RoleId = roleId }).ToList()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        user = (await FindUser(user.Id, false))!;
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ApiResponse<object>.Ok(ToResponse(user)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request)
    {
        var user = await FindUser(id, false);
        if (user is null) return ApiResults.Error(this, StatusCodes.Status404NotFound, "User not found.");
        var validation = await ValidateReferences(request.DepartmentId, request.RoleIds);
        if (validation is not null) return validation;

        user.FullName = request.FullName.Trim();
        user.Email = request.Email?.Trim();
        user.Phone = request.Phone?.Trim();
        user.DepartmentId = request.DepartmentId;
        user.UpdatedAt = DateTime.UtcNow;
        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = request.RoleIds.Distinct().Select(roleId => new UserRole { UserId = id, RoleId = roleId }).ToList();
        await db.SaveChangesAsync();
        user = (await FindUser(id, false))!;
        return Ok(ApiResponse<object>.Ok(ToResponse(user)));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateUserStatusRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return ApiResults.Error(this, StatusCodes.Status404NotFound, "User not found.");
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId && !request.IsActive)
            return ApiResults.Error(this, StatusCodes.Status409Conflict, "You cannot deactivate your own account.");
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        if (!request.IsActive)
        {
            var tokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync();
            foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { user.Id, user.IsActive }));
    }

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id) => UpdateStatus(id, new UpdateUserStatusRequest(false));

    private async Task<User?> FindUser(Guid id, bool noTracking)
    {
        var query = db.Users.Include(u => u.Department).Include(u => u.UserRoles).ThenInclude(ur => ur.Role);
        return noTracking ? await query.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            : await query.FirstOrDefaultAsync(u => u.Id == id);
    }

    private async Task<IActionResult?> ValidateReferences(Guid? departmentId, IReadOnlyList<Guid> roleIds)
    {
        if (departmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive))
            return ApiResults.Error(this, StatusCodes.Status400BadRequest, "Validation failed.", "departmentId: active department not found");
        var distinctRoles = roleIds.Distinct().ToArray();
        if (distinctRoles.Length != await db.Roles.CountAsync(r => distinctRoles.Contains(r.Id)))
            return ApiResults.Error(this, StatusCodes.Status400BadRequest, "Validation failed.", "roleIds: one or more roles do not exist");
        return null;
    }

    private static object ToResponse(User user) => new
    {
        user.Id, user.Username, user.FullName, user.Email, user.Phone, user.DepartmentId,
        departmentName = user.Department?.Name, user.IsActive,
        roles = user.UserRoles.Select(ur => new { ur.Role.Id, ur.Role.Name }),
        user.LastLoginAt, user.CreatedAt, user.UpdatedAt
    };
}
