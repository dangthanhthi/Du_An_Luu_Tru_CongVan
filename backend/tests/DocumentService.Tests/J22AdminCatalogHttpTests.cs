using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DocumentService.Tests;

public sealed class J22AdminCatalogHttpTests
{
    [Theory]
    [InlineData("Active", 1, 25, 20, "C00", "C19")]
    [InlineData("Active", 2, 25, 5, "C20", "C24")]
    [InlineData("Inactive", 1, 7, 7, "C25", "C31")]
    [InlineData("All", 2, 32, 12, "C20", "C31")]
    public async Task Admin_filter_precedes_count_and_stable_paging(string activity, int page, int total, int count, string first, string last)
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host);
        await Seed(host);
        var data = await Data(await client.GetAsync($"/api/v2/admin/catalogs?group=categories&activity={activity}&pageNumber={page}"));
        Assert.Equal("categories", data.GetProperty("group").GetString());
        Assert.Equal(activity, data.GetProperty("activity").GetString());
        Assert.Equal(total, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(page, data.GetProperty("pageNumber").GetInt32());
        Assert.Equal(20, data.GetProperty("pageSize").GetInt32());
        Assert.True(data.GetProperty("canEditGroup").GetBoolean());
        var items = data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(count, items.Length); Assert.Equal(first, items[0].GetProperty("code").GetString()); Assert.Equal(last, items[^1].GetProperty("code").GetString());
        if (activity != "All") Assert.All(items, x => Assert.Equal(activity == "Active", x.GetProperty("isActive").GetBoolean()));
        var empty = await Data(await client.GetAsync("/api/v2/admin/catalogs?group=categories&searchTerm=missing&pageNumber=1000000&pageSize=100"));
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32()); Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.Equal(1000000, empty.GetProperty("pageNumber").GetInt32());
    }

    [Fact]
    public async Task Capability_uses_signed_policy_and_valid_actor_reader_lookup_stays_active_only()
    {
        await using var host = new V2HttpTests.Host(); await Seed(host);
        using var manager = Manager(host); using var reader = host.Client(Guid.NewGuid()); using var anonymous = host.CreateClient();
        Assert.True((await Data(await manager.GetAsync("/api/v2/admin/catalogs/options"))).GetProperty("canManage").GetBoolean());
        await Error(await manager.GetAsync("/api/v2/admin/catalogs/options?group=categories"), HttpStatusCode.BadRequest, "INVALID_ADMIN_CATALOG_QUERY");
        var defaultPage = await Data(await manager.GetAsync("/api/v2/admin/catalogs?group=categories")); Assert.Equal("Active", defaultPage.GetProperty("activity").GetString()); Assert.Equal(25, defaultPage.GetProperty("totalCount").GetInt32());
        Assert.False((await Data(await reader.GetAsync("/api/v2/admin/catalogs/options"))).GetProperty("canManage").GetBoolean());
        await Status(await reader.GetAsync("/api/v2/admin/catalogs?group=categories&activity=All"), HttpStatusCode.Forbidden);
        foreach (var path in new[] { "/api/v2/admin/catalogs/options", "/api/v2/admin/catalogs?group=categories" }) await Status(await anonymous.GetAsync(path), HttpStatusCode.Unauthorized);
        using var invalidActor = Manager(host, "invalid-subject");
        await Error(await invalidActor.GetAsync("/api/v2/admin/catalogs/options"), HttpStatusCode.Unauthorized, "ACTOR_REQUIRED");
        await Error(await invalidActor.GetAsync("/api/v2/admin/catalogs?group=categories"), HttpStatusCode.Unauthorized, "ACTOR_REQUIRED");
        var lookup = await Data(await reader.GetAsync("/api/v2/catalogs?groups=categories")); Assert.Equal(25, lookup.GetProperty("categories").GetArrayLength());
        Assert.All(lookup.GetProperty("categories").EnumerateArray(), x => Assert.True(x.GetProperty("isActive").GetBoolean()));
    }

    [Theory]
    [InlineData("companies")]
    [InlineData("sensitivity")]
    public async Task Fixed_groups_are_browsable_but_updates_remain_rejected(string group)
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host);
        var page = await Data(await client.GetAsync($"/api/v2/admin/catalogs?group={group}&activity=All"));
        Assert.False(page.GetProperty("canEditGroup").GetBoolean()); var item = page.GetProperty("items")[0];
        await Error(await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{item.GetProperty("id").GetGuid()}", new { name = item.GetProperty("name").GetString(), sortOrder = 0, isActive = true, version = 1 }), HttpStatusCode.BadRequest, "FIXED_GROUP");
    }

    [Theory]
    [InlineData("")][InlineData("group=")][InlineData("group=methods,categories")][InlineData("group=unknown")]
    [InlineData("group=categories&activity=")][InlineData("group=categories&activity=active")][InlineData("group=categories&activity=Other")]
    [InlineData("group=categories&searchTerm=")][InlineData("group=categories&searchTerm=%20%20")]
    [InlineData("group=categories&pageSize=0")][InlineData("group=categories&pageSize=101")]
    [InlineData("group=categories&pageNumber=0")][InlineData("group=categories&pageNumber=1000001")]
    [InlineData("group=categories&pageNumber=1.5")][InlineData("group=categories&pageSize=abc")]
    [InlineData("group=categories&group=categories")][InlineData("group=categories&activity=All&activity=All")]
    [InlineData("group=categories&pageNumber=1&pageNumber=1")][InlineData("group=categories&pageSize=1&pageSize=1")]
    [InlineData("group=categories&searchTerm=x&searchTerm=x")][InlineData("group=categories&includeInactive=true")]
    [InlineData("Group=categories")][InlineData("group=categories&PAGESIZE=20")][InlineData("group=categories&pageSize=%2B1")]
    public async Task Malformed_unknown_or_duplicate_query_returns_catalog_error(string query)
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host);
        await Error(await client.GetAsync("/api/v2/admin/catalogs?" + query), HttpStatusCode.BadRequest, "INVALID_ADMIN_CATALOG_QUERY");
    }

    [Fact]
    public async Task Search_is_trimmed_literal_and_parameterized_with_unicode_and_sql_text()
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host); await Seed(host);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
            db.BusinessCatalogEntries.AddRange(new BusinessCatalogEntry { Group = "categories", Code = "LITERAL", Name = "Pháp lý %_[] ' OR 1=1 --" }, new BusinessCatalogEntry { Group = "categories", Code = "DECOY", Name = "Pháp lý ordinary" }); await db.SaveChangesAsync();
        }
        // SQLite LIKE folds ASCII only; SQL Server collation behavior is verified in the SQL fixture.
        foreach (var term in new[] { "%_[]", "' OR 1=1 --", "  pháp lý %_[]  ", "literal" })
        {
            var data = await Data(await client.GetAsync("/api/v2/admin/catalogs?group=categories&searchTerm=" + Uri.EscapeDataString(term)));
            Assert.Equal(1, data.GetProperty("totalCount").GetInt32()); Assert.Equal("LITERAL", Assert.Single(data.GetProperty("items").EnumerateArray()).GetProperty("code").GetString());
        }
        await Error(await client.GetAsync("/api/v2/admin/catalogs?group=categories&searchTerm=" + new string('x', 201)), HttpStatusCode.BadRequest, "INVALID_ADMIN_CATALOG_QUERY");
    }

    [Theory]
    [InlineData("versionZero")][InlineData("versionUnsafe")][InlineData("idEmpty")][InlineData("codeEmpty")][InlineData("codeInvalid")]
    [InlineData("nameEmpty")][InlineData("nameLong")][InlineData("orderNegative")][InlineData("orderLarge")]
    public async Task Invalid_stored_wire_data_returns_503_without_partial_page(string mode)
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); var entry = new BusinessCatalogEntry { Group = "categories", Code = "BAD", Name = "Stored fixture" };
            switch (mode)
            {
                case "versionZero": entry.Version = 0; break; case "versionUnsafe": entry.Version = 9007199254740992; break;
                case "idEmpty": entry.Id = Guid.Empty; break; case "codeEmpty": entry.Code = ""; break; case "codeInvalid": entry.Code = "bad/code"; break;
                case "nameEmpty": entry.Name = "  "; break; case "nameLong": entry.Name = new string('x', 201); break;
                case "orderNegative": entry.SortOrder = -1; break; case "orderLarge": entry.SortOrder = 10001; break;
            }
            db.BusinessCatalogEntries.Add(entry); await db.SaveChangesAsync();
            // EF generates keys for Guid.Empty on insert; corrupt the persisted fixture via SQL instead.
            if (mode == "idEmpty") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE BusinessCatalogEntries SET Id = {Guid.Empty} WHERE Id = {entry.Id}");
        }
        await Error(await client.GetAsync("/api/v2/admin/catalogs?group=categories"), HttpStatusCode.ServiceUnavailable, "CATALOG_DATA_INVALID");
    }

    [Fact]
    public async Task Reactivation_audit_failure_rolls_back_entry_and_version()
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host); await Seed(host);
        Guid id;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); id = (await db.BusinessCatalogEntries.SingleAsync(x => x.Group == "categories" && x.Code == "C25")).Id;
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailCatalogAudit BEFORE INSERT ON CatalogAuditEvents BEGIN SELECT RAISE(ABORT, 'Synthetic audit write failure'); END;");
        }
        await Status(await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", new { name = "Fixture 25", sortOrder = 2, isActive = true, version = 1 }), HttpStatusCode.InternalServerError);
        using var verifyScope = host.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<DocumentDbContext>(); var row = await verify.BusinessCatalogEntries.SingleAsync(x => x.Id == id);
        Assert.False(row.IsActive); Assert.Equal(1, row.Version); Assert.Equal("Fixture 25", row.Name); Assert.Equal(2, row.SortOrder); Assert.Empty(await verify.CatalogAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Reactivation_keeps_identity_metadata_and_one_audit_stale_writer_and_duplicate_inactive_are_409()
    {
        await using var host = new V2HttpTests.Host(); using var client = Manager(host); await Seed(host);
        var page = await Data(await client.GetAsync("/api/v2/admin/catalogs?group=categories&activity=Inactive")); var row = page.GetProperty("items")[0]; var id = row.GetProperty("id").GetGuid();
        await Error(await client.PostAsJsonAsync("/api/v2/admin/catalogs", new { group = "categories", code = "C25", name = "Duplicate" }), HttpStatusCode.Conflict, "DUPLICATE_CODE");
        var draft = new { name = "Fixture 25", sortOrder = 2, isActive = true, version = 1 };
        var changed = await Data(await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", draft));
        Assert.Equal(id, changed.GetProperty("id").GetGuid()); Assert.Equal("C25", changed.GetProperty("code").GetString()); Assert.Equal("Fixture 25", changed.GetProperty("name").GetString()); Assert.Equal(2, changed.GetProperty("sortOrder").GetInt32()); Assert.True(changed.GetProperty("isActive").GetBoolean()); Assert.Equal(2, changed.GetProperty("version").GetInt64());
        await Error(await client.PutAsJsonAsync($"/api/v2/admin/catalogs/{id}", draft), HttpStatusCode.Conflict, "VERSION_CONFLICT");
        var historical = await Data(await client.GetAsync($"/api/v2/catalogs/{id}")); Assert.Equal(2, historical.GetProperty("version").GetInt64());
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
        var audit = Assert.Single(await db.CatalogAuditEvents.Where(x => x.EntryId == id).ToListAsync()); Assert.Equal("Updated", audit.Action);
        using var before = JsonDocument.Parse(audit.BeforeJson); using var after = JsonDocument.Parse(audit.AfterJson); Assert.False(before.RootElement.GetProperty("IsActive").GetBoolean()); Assert.True(after.RootElement.GetProperty("IsActive").GetBoolean());
    }

    internal static async Task Seed(V2HttpTests.Host host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
        db.BusinessCatalogEntries.AddRange(Enumerable.Range(0, 32).Select(i => new BusinessCatalogEntry { Group = "categories", Code = $"C{i:00}", Name = $"Fixture {i}", IsActive = i < 25, SortOrder = i / 10 })); await db.SaveChangesAsync();
    }
    internal static HttpClient Manager(V2HttpTests.Host host, string? subject = null)
    {
        var client = host.CreateClient(); var token = new JwtSecurityToken(claims: [new("sub", subject ?? Guid.NewGuid().ToString()), new("das_capability", "CatalogManage")], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(V2HttpTests.Host.Key)), SecurityAlgorithms.HmacSha256)); client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return client;
    }
    private static async Task Status(HttpResponseMessage response, HttpStatusCode expected)
    { Assert.Equal(expected, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl?.ToString()); await response.Content.ReadAsStringAsync(); }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        await Status(response, HttpStatusCode.OK); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var root = json.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean()); Assert.Equal(JsonValueKind.Null, root.GetProperty("message").ValueKind); Assert.Empty(root.GetProperty("errors").EnumerateArray()); Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString())); return root.GetProperty("data").Clone();
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        await Status(response, expected); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var root = json.RootElement; Assert.False(root.GetProperty("success").GetBoolean()); Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind); Assert.Equal(code, Assert.Single(root.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString()); Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }
}
