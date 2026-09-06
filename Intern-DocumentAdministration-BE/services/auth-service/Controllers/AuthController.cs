using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthDbContext db, ITokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var username = request.Username.Trim();
        var user = await db.Users
            .Include(u => u.Department)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ApiResults.Error(this, StatusCodes.Status401Unauthorized, "Invalid username or password.");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = RefreshTokenHasher.Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            user = ToCurrentUser(user, roles)
        }));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t =>
            (t.Token == tokenHash || t.Token == request.RefreshToken) && t.RevokedAt == null);

        if (stored is null || stored.ExpiresAt <= DateTime.UtcNow)
            return ApiResults.Error(this, StatusCodes.Status401Unauthorized, "Refresh token is invalid or expired.");

        var user = await db.Users
            .Include(u => u.Department)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == stored.UserId && u.IsActive);

        if (user is null)
            return ApiResults.Error(this, StatusCodes.Status401Unauthorized, "User is inactive or no longer exists.");

        stored.RevokedAt = DateTime.UtcNow;
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = RefreshTokenHasher.Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { accessToken, refreshToken }));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var userId = GetCurrentUserId();
        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t =>
            (t.Token == tokenHash || t.Token == request.RefreshToken) &&
            t.UserId == userId && t.RevokedAt == null);

        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(ApiResponse<object>.Ok(null));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == GetCurrentUserId() && u.IsActive);

        if (user is null)
            return ApiResults.Error(this, StatusCodes.Status404NotFound, "User not found.");

        return Ok(ApiResponse<object>.Ok(ToCurrentUser(user, user.UserRoles.Select(ur => ur.Role.Name))));
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static object ToCurrentUser(User user, IEnumerable<string> roles) => new
    {
        id = user.Id,
        fullName = user.FullName,
        email = user.Email,
        role = roles.FirstOrDefault(),
        roles,
        departmentId = user.DepartmentId,
        departmentName = user.Department?.Name
    };
}
