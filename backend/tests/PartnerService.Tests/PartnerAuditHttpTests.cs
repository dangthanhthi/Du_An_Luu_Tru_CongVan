using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerAuditHttpTests
{
    [Fact]
    public async Task Exhausted_database_retries_return_sanitized_unavailable_envelope()
    {
        var interceptor=new RetryFailure(); await using var h=new PartnerHttpTests.Host(interceptor); using var c=h.Client(true);
        var id=(await PartnerHttpTests.Data(await c.PostAsJsonAsync("/api/partners",new {fullName="Private"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();
        interceptor.Enabled=true;
        var response=await c.GetAsync($"/api/partners/{id}/audit");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
        var body=await response.Content.ReadAsStringAsync();
        Assert.Contains("PARTNER_DEPENDENCY_UNAVAILABLE",body); Assert.DoesNotContain("PRIVATE_SQL_SENTINEL",body);
    }
    private sealed class RetryFailure:Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Enabled;
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command,Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,CancellationToken ct=default)
        {
            if(Enabled)throw new Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException("PRIVATE_SQL_SENTINEL",new TimeoutException("PRIVATE_SQL_SENTINEL"));
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Real_writer_events_and_soft_deleted_partner_are_readable_only_by_manager()
    {
        await using var h = new PartnerHttpTests.Host(); using var c = h.Client(true);
        var id = (await PartnerHttpTests.Data(await c.PostAsJsonAsync("/api/partners", new { fullName = "History" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await PartnerHttpTests.Data(await c.PutAsJsonAsync($"/api/partners/{id}", new { fullName = "Updated", expectedVersion = 1 }));
        await PartnerHttpTests.Data(await PartnerHttpTests.Change(c,id,"DELETE",2));
        var deleted = await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit"));
        Assert.Equal("3", deleted.GetProperty("throughVersion").GetString());
        await PartnerHttpTests.Data(await c.PostAsJsonAsync($"/api/partners/{id}/restore", new { expectedVersion = 3 }));
        var page = await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit"));
        Assert.Equal(id, page.GetProperty("partnerId").GetGuid()); Assert.Equal(4, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { "Restore", "Delete", "Update", "Create" }, page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("action").GetString()));
        Assert.All(page.GetProperty("items").EnumerateArray(), item => {
            Assert.Equal(h.Actor,item.GetProperty("actorUserId").GetGuid()); Assert.Equal("NotRecorded",item.GetProperty("changesAvailability").GetString());
            Assert.EndsWith("Z",item.GetProperty("occurredAt").GetString()); Assert.False(item.TryGetProperty("changedFields",out _)); Assert.False(item.TryGetProperty("actorName",out _));
        });
        using var reader=h.Client(false); using var noSubject=h.Client(true,subject:false); using var anonymous=h.CreateClient();
        foreach(var candidate in new[] {id,Guid.NewGuid()}) {
            Assert.Equal(HttpStatusCode.Forbidden,(await reader.GetAsync($"/api/partners/{candidate}/audit")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await noSubject.GetAsync($"/api/partners/{candidate}/audit")).StatusCode);
            var challenge=await anonymous.GetAsync($"/api/partners/{candidate}/audit");
            Assert.Equal(HttpStatusCode.Unauthorized,challenge.StatusCode); Assert.Equal("no-store",challenge.Headers.CacheControl?.ToString());
        }
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/partners/{Guid.NewGuid()}/audit")).StatusCode);
    }
    [Fact]
    public async Task Watermark_keeps_page_two_stable_after_new_event_and_refresh_sees_it()
    {
        await using var h=new PartnerHttpTests.Host(); using var c=h.Client(true);
        var id=(await PartnerHttpTests.Data(await c.PostAsJsonAsync("/api/partners",new {fullName="A"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();
        for(var i=1;i<4;i++) await PartnerHttpTests.Data(await c.PutAsJsonAsync($"/api/partners/{id}",new {fullName="A"+i,expectedVersion=i}));
        var first=await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit?pageSize=2")); Assert.Equal("4",first.GetProperty("throughVersion").GetString());
        await PartnerHttpTests.Data(await c.PutAsJsonAsync($"/api/partners/{id}",new {fullName="A5",expectedVersion=4}));
        var second=await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit?pageSize=2&pageNumber=2&throughVersion=4"));
        Assert.Equal(4,second.GetProperty("totalCount").GetInt32()); Assert.Equal(new[]{"2","1"},second.GetProperty("items").EnumerateArray().Select(x=>x.GetProperty("version").GetString()));
        Assert.Equal("5",(await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit"))).GetProperty("throughVersion").GetString());
    }
    [Theory]
    [InlineData("throughVersion=01")][InlineData("throughVersion=0")][InlineData("throughVersion=%2B1")][InlineData("throughVersion=9223372036854775808")]
    [InlineData("throughVersion=2")][InlineData("pageSize=101")][InlineData("pageNumber=0")][InlineData("pageNumber=2147483647")]
    [InlineData("pageSize=1&pageSize=2")][InlineData("unknown=1")][InlineData("pageNumber=2")]
    public async Task Invalid_or_unanchored_query_returns_400(string query)
    {
        await using var h=new PartnerHttpTests.Host(); using var c=h.Client(true);
        var id=(await PartnerHttpTests.Data(await c.PostAsJsonAsync("/api/partners",new {fullName="A"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var response=await c.GetAsync($"/api/partners/{id}/audit?{query}"); Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Contains("INVALID_HISTORY_QUERY",await response.Content.ReadAsStringAsync()); Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
    }
    [Fact]
    public async Task Int64_versions_above_javascript_safe_integer_remain_exact_strings()
    {
        await using var h=new PartnerHttpTests.Host(); using var c=h.Client(true); var id=Guid.NewGuid();
        using(var scope=h.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<PartnerDbContext>(); db.Partners.Add(new Partner {Id=id,FullName="Large",CreatedByUserId=h.Actor,Version=long.MaxValue});
            db.PartnerAudits.Add(new PartnerAudit {PartnerId=id,ActorUserId=h.Actor,Version=long.MaxValue,Action="Update",CreatedAt=DateTime.UtcNow}); await db.SaveChangesAsync();
        }
        var data=await PartnerHttpTests.Data(await c.GetAsync($"/api/partners/{id}/audit?throughVersion=9223372036854775807"));
        Assert.Equal("9223372036854775807",data.GetProperty("throughVersion").GetString()); Assert.Equal("9223372036854775807",data.GetProperty("items")[0].GetProperty("version").GetString());
    }
    [Theory]
    [InlineData("unknown")][InlineData("actor")][InlineData("time")]
    public async Task Invalid_stored_event_fails_closed_without_leaking_data(string defect)
    {
        await using var h=new PartnerHttpTests.Host(); using var c=h.Client(true);
        var id=(await PartnerHttpTests.Data(await c.PostAsJsonAsync("/api/partners",new {fullName="Private"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using(var scope=h.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<PartnerDbContext>(); var a=await db.PartnerAudits.SingleAsync();
            if(defect=="unknown")a.Action="PRIVATE_BAD_ACTION"; if(defect=="actor")a.ActorUserId=Guid.Empty; if(defect=="time")a.CreatedAt=default; await db.SaveChangesAsync();}
        var response=await c.GetAsync($"/api/partners/{id}/audit"); Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync(); Assert.Contains("HISTORY_DATA_INVALID",body); Assert.DoesNotContain("PRIVATE_BAD_ACTION",body); Assert.DoesNotContain("Private",body);
    }
}
