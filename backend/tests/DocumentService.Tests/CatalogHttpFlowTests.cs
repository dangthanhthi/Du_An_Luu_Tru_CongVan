using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DocumentService.Tests;

public sealed class CatalogHttpFlowTests
{
    [Fact]
    public async Task Catalog_http_create_edit_deactivate_retains_historical_identity_and_audit()
    {
        await using var host = new V2HttpTests.Host();
        var actor = Guid.NewGuid(); using var client = Manager(host, actor);
        var created = await Data(await client.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "categories", code = " local ", name = " Local fixture " }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("LOCAL", created.GetProperty("code").GetString());
        Assert.Equal(1, created.GetProperty("version").GetInt64());
        var edit = new { name = "Changed fixture", version = 1, sortOrder = 4, isActive = false };
        var changed = await Data(await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", edit));
        Assert.Equal(2, changed.GetProperty("version").GetInt64());
        Assert.False(changed.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", edit)).StatusCode);
        var historical = await Data(await client.GetAsync($"/api/v2/catalogs/{id}"));
        Assert.Equal("Changed fixture", historical.GetProperty("name").GetString());
        Assert.Equal("LOCAL", historical.GetProperty("code").GetString());
        var active = await Data(await client.GetAsync("/api/v2/catalogs?groups=categories"));
        Assert.DoesNotContain(active.GetProperty("categories").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "categories", code = "LOCAL", name = "Duplicate fixture" })).StatusCode);
        using var scope = host.Services.CreateScope();
        var audits = await scope.ServiceProvider.GetRequiredService<DocumentDbContext>().Set<CatalogAuditEvent>().Where(x => x.EntryId == id).ToListAsync();
        Assert.Equal(2, audits.Count); Assert.All(audits, x => Assert.Equal(actor, x.ActorUserId));
    }

    [Fact]
    public async Task Catalog_http_generic_admin_cannot_mutate_and_anonymous_cannot_read()
    {
        await using var host = new V2HttpTests.Host();
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v2/catalogs?groups=methods")).StatusCode);
        using var admin = host.Client(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "methods", code = "LOCAL", name = "Fixture" })).StatusCode);
    }

    [Fact]
    public async Task Catalog_http_manager_cannot_modify_fixed_groups_or_accept_invalid_order()
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "companies", code = "LOCAL", name = "Fixture" })).StatusCode);
        var created = await Data(await client.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "categories", code = "ORDER", name = "Fixture" }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", new { name = "Changed", version = 1, sortOrder = -1, isActive = true })).StatusCode);
        Assert.Equal(1, (await Data(await client.GetAsync($"/api/v2/catalogs/{id}"))).GetProperty("version").GetInt64());
    }

    private static HttpClient Manager(V2HttpTests.Host host, Guid user)
    {
        var client = host.CreateClient();
        // Signed capability belongs to this isolated host, never a browser role override.
        var token = new JwtSecurityToken(claims: [new("sub", user.ToString()), new("das_capability", "CatalogManage")], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(V2HttpTests.Host.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
