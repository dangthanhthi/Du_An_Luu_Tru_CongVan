using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthService.Tests;

public sealed class UserDeletionTests
{
    [Fact]
    public async Task Delete_soft_deletes_user_and_default_list_stays_filtered_after_refresh()
    {
        var options = CreateOptions();
        var currentAdmin = NewUser("current-admin");
        var target = NewUser("delete-target");
        await using var db = new AuthDbContext(options);
        db.Users.AddRange(currentAdmin, target);
        await db.SaveChangesAsync();
        var controller = CreateController(db, currentAdmin.Id);

        var deleteResult = await controller.Delete(target.Id);

        Assert.IsType<OkObjectResult>(deleteResult);
        Assert.True(target.IsDeleted);
        Assert.False(target.IsActive);
        Assert.NotNull(target.DeletedAt);

        var firstList = GetPagedResult(await controller.GetAll());
        Assert.Equal(1, firstList.TotalCount);

        db.ChangeTracker.Clear();
        var refreshedController = CreateController(db, currentAdmin.Id);
        var refreshedList = GetPagedResult(await refreshedController.GetAll());
        Assert.Equal(1, refreshedList.TotalCount);
        Assert.True(await db.Users.AnyAsync(user => user.Id == target.Id && user.IsDeleted));
    }

    [Fact]
    public async Task Delete_nonexistent_or_already_deleted_user_returns_not_found()
    {
        await using var db = new AuthDbContext(CreateOptions());
        var currentAdmin = NewUser("current-admin");
        db.Users.Add(currentAdmin);
        await db.SaveChangesAsync();
        var controller = CreateController(db, currentAdmin.Id);

        Assert.Equal(StatusCodes.Status404NotFound, GetStatusCode(await controller.Delete(Guid.NewGuid())));

        var target = NewUser("deleted-target");
        db.Users.Add(target);
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.Delete(target.Id));
        Assert.Equal(StatusCodes.Status404NotFound, GetStatusCode(await controller.Delete(target.Id)));
    }

    [Fact]
    public async Task Current_admin_cannot_delete_own_account()
    {
        await using var db = new AuthDbContext(CreateOptions());
        var currentAdmin = NewUser("current-admin");
        db.Users.Add(currentAdmin);
        await db.SaveChangesAsync();

        var result = await CreateController(db, currentAdmin.Id).Delete(currentAdmin.Id);

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(result));
        Assert.False(currentAdmin.IsDeleted);
        Assert.True(currentAdmin.IsActive);
    }

    private static DbContextOptions<AuthDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static User NewUser(string username) => new()
    {
        Username = username,
        PasswordHash = "test-hash",
        FullName = username,
        IsActive = true
    };

    private static UsersController CreateController(AuthDbContext db, Guid currentUserId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        ], "Test"));
        return new UsersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static PagedResult<object> GetPagedResult(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        return Assert.IsType<PagedResult<object>>(response.Data);
    }

    private static int GetStatusCode(IActionResult result) =>
        Assert.IsType<ObjectResult>(result).StatusCode ?? 0;
}
