using System.Security.Claims;
using AuthService.Organization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace AuthService.Tests;

public sealed class DirectoryApiTests
{
    [Fact]
    public async Task Staff_lookup_returns_only_own_department_and_groups()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        var result = Assert.IsType<OkObjectResult>(await Controller(store, DirectoryPersistenceTests.Staff).Departments(true));
        var page = Assert.IsType<ApiResponse<DirectoryPage<DirectoryUnit>>>(result.Value).Data!;
        Assert.Equal(new[] { DirectoryPersistenceTests.Finance, DirectoryPersistenceTests.Group }.Order(), page.Items.Select(x => x.Id).Order());
        Assert.Equal(2, page.TotalCount);
    }
    [Fact]
    public async Task Technical_Admin_role_never_grants_directory_read_all()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        var controller = Controller(store, DirectoryPersistenceTests.Staff, roles: ["Admin"]);
        var result = Assert.IsType<OkObjectResult>(await controller.Departments());
        var page = Assert.IsType<ApiResponse<DirectoryPage<DirectoryUnit>>>(result.Value).Data!;
        Assert.Single(page.Items);
        Assert.DoesNotContain(page.Items, x => x.Id == DirectoryPersistenceTests.Management);
    }
    [Fact]
    public async Task Explicit_directory_capability_allows_paged_read_but_unknown_actor_still_denied()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        var result = Assert.IsType<OkObjectResult>(await Controller(store, DirectoryPersistenceTests.Staff, ["DirectoryReadAll"]).Departments(pageSize: 1));
        var page = Assert.IsType<ApiResponse<DirectoryPage<DirectoryUnit>>>(result.Value).Data!;
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        var denied = Assert.IsType<ObjectResult>(await Controller(store, Guid.NewGuid(), ["DirectoryReadAll"]).Departments());
        Assert.Equal(403, denied.StatusCode);
    }
    [Fact]
    public async Task Originator_lookup_requires_allowed_department_and_returns_minimal_identity()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        var controller = Controller(store, DirectoryPersistenceTests.Staff);
        var result = Assert.IsType<OkObjectResult>(await controller.Users(DirectoryPersistenceTests.Finance));
        var page = Assert.IsType<ApiResponse<DirectoryPage<DirectoryUserLookup>>>(result.Value).Data!;
        Assert.Equal(DirectoryPersistenceTests.Staff, Assert.Single(page.Items).Id);
        var forbidden = Assert.IsType<ObjectResult>(await controller.Users(DirectoryPersistenceTests.Management));
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }
    [Fact]
    public async Task Invalid_paging_or_lookup_purpose_is_400()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var c = Controller(new OrganizationDirectoryStore(db, fixture.Clock), DirectoryPersistenceTests.Staff);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(await c.Departments(pageSize: 101)).StatusCode);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(await c.Departments(pageNumber: 0)).StatusCode);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(await c.Users(DirectoryPersistenceTests.Finance, "dump-all")).StatusCode);
    }
    [Fact]
    public async Task Missing_or_stale_projection_is_503_instead_of_successful_empty_list()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        var c = Controller(store, DirectoryPersistenceTests.Staff);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await c.Departments()).StatusCode);
        await store.ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        fixture.Clock.Now += TimeSpan.FromSeconds(61);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await c.Users(DirectoryPersistenceTests.Finance)).StatusCode);
    }
    [Fact]
    public async Task Anonymous_or_unparseable_actor_is_401()
    {
        await using var fixture = await DirectoryPersistenceTests.Fixture.Create();
        await using var db = fixture.Db();
        var c = Controller(new OrganizationDirectoryStore(db, fixture.Clock), Guid.Empty);
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Equal(401, Assert.IsType<ObjectResult>(await c.Departments()).StatusCode);
    }
    internal static DirectoryController Controller(OrganizationDirectoryStore store, Guid actor, string[]? capabilities = null, string[]? roles = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actor.ToString()) };
        claims.AddRange((capabilities ?? []).Select(x => new Claim("das_capability", x)));
        claims.AddRange((roles ?? []).Select(x => new Claim(ClaimTypes.Role, x)));
        return new DirectoryController(store, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Directory:SourceId"] = "eap" }).Build())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } } };
    }
}

