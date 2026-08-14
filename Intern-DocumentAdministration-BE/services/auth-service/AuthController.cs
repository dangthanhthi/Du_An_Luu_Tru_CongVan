using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthService;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(AuthDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { success = false, message = "Sai tài khoản hoặc mật khẩu" });
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                accessToken,
                refreshToken,
                expiresIn = 3600,
                user = new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    role = roles.FirstOrDefault(),
                    departmentId = user.DepartmentId
                }
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.RevokedAt == null);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new { success = false, message = "Refresh token không hợp lệ hoặc đã hết hạn" });
        }

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == stored.UserId && u.IsActive);

        if (user is null)
        {
            return Unauthorized(new { success = false, message = "Tài khoản không tồn tại hoặc đã bị khóa" });
        }

        stored.RevokedAt = DateTime.UtcNow;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = new { accessToken = newAccessToken, refreshToken = newRefreshToken } });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(claim, out var currentUserId);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == currentUserId);

        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok(new { success = true, data = (object?)null });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            return Unauthorized(new { success = false, message = "Không thể xác định danh tính người dùng." });

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return NotFound();

        return Ok(new
        {
            success = true,
            data = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                role = user.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                departmentId = user.DepartmentId
            }
        });
    }

    [HttpGet("/api/users")]
    [Authorize]
    public async Task<IActionResult> GetUserByRole([FromQuery] string? role)
    {
        if (string.IsNullOrEmpty(role))
            return BadRequest(new { success = false, message = "Tham số 'role' là bắt buộc." });

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserRoles.Any(ur => ur.Role.Name == role) && u.IsActive);

        if (user is null)
            return NotFound(new { success = false, message = $"Không tìm thấy user nào có role: {role}" });

        return Ok(new
        {
            success = true,
            data = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                role = user.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                departmentId = user.DepartmentId
            }
        });
    }
}
