using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocumentService.Tests;

public sealed class LegacyAdminHttpTests
{
    [Theory][InlineData("INCOMING")][InlineData("OUTGOING")][InlineData("INTERNAL")]
    public async Task Admin_token_does_not_bypass_legacy_list_detail_file_or_mutation_routes(string kind)
    {
        var user = Guid.NewGuid();var id = Guid.NewGuid();var file = Guid.NewGuid();var department = Guid.NewGuid();
        await using var host = new V2HttpTests.Host();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
            var doc = new Document { Id = id, DocType = kind, DocumentNumber = "Historical-number", Title = "Private historical subject", Status = "Draft", CreatedByUserId = Guid.NewGuid(), SenderDepartmentId = department };
            doc.Attachments.Add(new DocumentAttachment { FileId = file });db.Add(doc);await db.SaveChangesAsync();
        }
        using var client = host.Client(user); // Admin role, without business grants.
        var list = await client.GetAsync("/api/documents");Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());Assert.Equal(0, body.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/documents/access/files/{file}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "Changed", senderDepartmentId = department })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/documents/{id}/status", new { status = "Reviewed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/documents/{id}")).StatusCode);
        using var check = host.Services.CreateScope();var unchanged = await check.ServiceProvider.GetRequiredService<DocumentDbContext>().Documents.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Private historical subject", unchanged.Title);Assert.Equal("Draft", unchanged.Status);Assert.False(unchanged.IsDeleted);
    }
}
