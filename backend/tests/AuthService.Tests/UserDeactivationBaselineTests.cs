using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthService.Tests;

[Trait("Suite", "LegacyBaseline")]
public sealed class UserDeactivationBaselineTests
{
    [Fact]
    public async Task Deactivation_persists_and_revokes_active_refresh_tokens()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var adminId = Guid.NewGuid();
        var target = new User { Username = "test-target", PasswordHash = "fixture-only", FullName = "Fixture" };
        var activeToken = new RefreshToken { UserId = target.Id, Token = "fixture-token", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        await using (var db = new AuthDbContext(options))
        {
            db.Users.Add(target);
            db.RefreshTokens.Add(activeToken);
            await db.SaveChangesAsync();
            var controller = new UsersController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, adminId.ToString())], "Test"))
                    }
                }
            };
            Assert.IsType<OkObjectResult>(await controller.UpdateStatus(target.Id, new UpdateUserStatusRequest(false)));
        }
        await using var reload = new AuthDbContext(options);
        Assert.False((await reload.Users.SingleAsync(u => u.Id == target.Id)).IsActive);
        Assert.NotNull((await reload.RefreshTokens.SingleAsync(t => t.UserId == target.Id)).RevokedAt);
    }
}
