using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocumentService.Tests;

public sealed class DocumentHistoryHttpTests
{
    [Fact]
    public async Task Exhausted_database_retries_return_sanitized_unavailable_envelope()
    {
        var a=new V2HttpTests.Authority(); var interceptor=new RetryFailure(); await using var h=new V2HttpTests.Host(a,interceptor:interceptor); using var c=h.Client(a.User);
        var id=await Create(c,a); interceptor.Enabled=true;
        var response=await c.GetAsync($"/api/v2/documents/{id}/history");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
        var body=await response.Content.ReadAsStringAsync();
        Assert.Contains("DOCUMENT_SERVICE_UNAVAILABLE",body); Assert.DoesNotContain("PRIVATE_SQL_SENTINEL",body);
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
    public async Task Real_lifecycle_cycles_keep_individual_reasons_and_restore_states_after_latest_cancellation_changes()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User);
        var id=await Create(c,a); long version=1;
        foreach(var step in new[]{("Cancel","Lý do A"),("Restore",(string?)null),("Distribute",(string?)null),("Cancel","Lý do B"),("Restore",(string?)null)})
            await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=version++,action=step.Item1,reason=step.Item2}));
        var page=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history"));
        Assert.Equal("V2LifecycleOnly",page.GetProperty("coverage").GetString()); Assert.Equal("6",page.GetProperty("throughVersion").GetString()); Assert.Equal(5,page.GetProperty("totalCount").GetInt32());
        var rows=page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[]{"Restore","Cancel","Distribute","Restore","Cancel"},rows.Select(x=>x.GetProperty("action").GetString()));
        Assert.Equal(new[]{"Lý do B","Lý do B",null,"Lý do A","Lý do A"},rows.Select(x=>x.GetProperty("cancellationReason").GetString()));
        Assert.Equal("Distributed",rows[0].GetProperty("toStatus").GetString()); Assert.Equal("InProgress",rows[3].GetProperty("toStatus").GetString());
        Assert.All(rows,x=>{ Assert.Equal(a.User,x.GetProperty("actorUserId").GetGuid()); Assert.EndsWith("Z",x.GetProperty("occurredAt").GetString()); });
        // Repeating distribution is a no-op: it must not add an event or advance the watermark.
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=6,action="Distribute"}));
        Assert.Equal(5,(await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history"))).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Metadata_edit_is_excluded_before_count_and_pagination_with_stable_watermark()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        await V2HttpTests.Data(await c.PutAsJsonAsync($"/api/v2/documents/{id}",new {expectedVersion=1,companyCode="HL",subject="Edited",originatorUserId=a.User,ownerDepartmentId=a.Department,sensitivity="Normal"}));
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=2,action="Cancel",reason="A"}));
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=3,action="Restore"}));
        var first=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history?pageSize=1")); Assert.Equal(2,first.GetProperty("totalCount").GetInt32()); Assert.Equal("4",first.GetProperty("throughVersion").GetString());
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=4,action="Cancel",reason="B"}));
        var second=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history?pageSize=1&pageNumber=2&throughVersion=4"));
        Assert.Equal(2,second.GetProperty("totalCount").GetInt32()); Assert.Equal("3",second.GetProperty("items")[0].GetProperty("version").GetString()); Assert.Equal("A",second.GetProperty("items")[0].GetProperty("cancellationReason").GetString());
    }
    [Fact]
    public async Task Unavailable_authority_inactive_actor_and_inaccessible_ids_fail_closed()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        a.Readable.Clear(); Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{Guid.NewGuid()}/history")).StatusCode);
        a.Readable.Add(id); a.Active=false; Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/v2/documents/{id}/history")).StatusCode);
        a.Active=true; a.Offline=true; Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync($"/api/v2/documents/{id}/history")).StatusCode);
        using var anonymous=h.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/api/v2/documents/{id}/history")).StatusCode);
    }
    [Fact]
    public async Task Fresh_authority_after_read_can_revoke_access_before_returning_private_history()
    {
        var a=new ChangingAuthority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.Inner.User); var id=await Create(c,a.Inner);
        var response=await c.GetAsync($"/api/v2/documents/{id}/history"); Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
        Assert.Equal(2,a.Reads); Assert.DoesNotContain("items",await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("{")][InlineData("[]")][InlineData("{\"Status\":{\"before\":\"InProgress\",\"after\":\"Unknown\"}}")]
    [InlineData("{\"Status\":null}")][InlineData("{\"Status\":{},\"Status\":{}}")]
    public async Task Malformed_payload_is_not_silently_dropped_even_when_off_the_requested_page(string payload)
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        using(var scope=h.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); var header=await db.DocumentRegistrations.SingleAsync(); header.Version=2;
            db.Set<DocumentEditAudit>().Add(new DocumentEditAudit {DocumentId=id,ActorUserId=a.User,Version=2,ChangedAt=DateTimeOffset.UtcNow,ChangesJson=payload}); await db.SaveChangesAsync();}
        var response=await c.GetAsync($"/api/v2/documents/{id}/history?pageNumber=2&pageSize=1&throughVersion=2");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode); Assert.Contains("HISTORY_DATA_INVALID",await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("pageNumber=0")][InlineData("pageNumber=2")][InlineData("pageSize=101")][InlineData("pageSize=1&pageSize=2")]
    [InlineData("throughVersion=01")][InlineData("throughVersion=0")][InlineData("throughVersion=2")][InlineData("throughVersion=9223372036854775808")][InlineData("actorUserId=1")]
    public async Task Invalid_history_query_is_rejected(string query)
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        var response=await c.GetAsync($"/api/v2/documents/{id}/history?{query}"); Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); Assert.Contains("INVALID_HISTORY_QUERY",await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Maximum_unicode_reasons_from_existing_writer_remain_readable_and_do_not_require_schema_changes()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a); var reason=new string('ế',4000);
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=1,action="Cancel",reason}));
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=2,action="Restore"}));
        var data=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history")); Assert.All(data.GetProperty("items").EnumerateArray(),x=>Assert.Equal(reason,x.GetProperty("cancellationReason").GetString()));
    }
    private static async Task<Guid> Create(HttpClient c,V2HttpTests.Authority a) {var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),Guid.NewGuid().ToString())).GetProperty("id").GetGuid(); a.Readable.Add(id); return id;}
    [Theory]
    [InlineData("reason")][InlineData("snapshotActor")][InlineData("eventActor")][InlineData("snapshotTime")][InlineData("eventTime")][InlineData("previousStatus")][InlineData("restorer")]
    public async Task Corrupt_snapshot_provenance_is_rejected_without_raw_payload(string defect)
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=1,action="Cancel",reason="Private cancellation reason"}));
        using(var scope=h.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); var audit=await db.Set<DocumentEditAudit>().SingleAsync();
            var root=System.Text.Json.Nodes.JsonNode.Parse(audit.ChangesJson)!; var snapshot=root["Cancellation"]!["after"]!;
            if(defect=="reason") snapshot["Reason"]=""; if(defect=="snapshotActor")snapshot["CancelledByUserId"]=Guid.Empty.ToString();
            if(defect=="eventActor")audit.ActorUserId=Guid.NewGuid(); if(defect=="snapshotTime")snapshot["CancelledAt"]="2026-10-09T01:00:00+07:00";
            if(defect=="eventTime")audit.ChangedAt=audit.ChangedAt.ToOffset(TimeSpan.FromHours(7)); if(defect=="previousStatus")snapshot["PreviousStatus"]="Distributed";
            if(defect=="restorer")snapshot["RestoredByUserId"]=a.User.ToString(); audit.ChangesJson=root.ToJsonString(); await db.SaveChangesAsync();
        }
        var response=await c.GetAsync($"/api/v2/documents/{id}/history"); Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync(); Assert.Contains("HISTORY_DATA_INVALID",body); Assert.DoesNotContain("Private cancellation",body);
    }
    [Fact]
    public async Task Large_version_history_is_exact_and_legacy_status_rows_are_not_fabricated()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        var empty=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history")); Assert.Equal(0,empty.GetProperty("totalCount").GetInt32());
        await V2HttpTests.Data(await c.PostAsJsonAsync($"/api/v2/documents/{id}/status",new {expectedVersion=1,action="Cancel",reason="A"}));
        using(var scope=h.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); (await db.DocumentRegistrations.SingleAsync()).Version=long.MaxValue; (await db.Set<DocumentEditAudit>().SingleAsync()).Version=long.MaxValue; await db.SaveChangesAsync();}
        var result=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history")); Assert.Equal("9223372036854775807",result.GetProperty("throughVersion").GetString()); Assert.Equal("9223372036854775807",result.GetProperty("items")[0].GetProperty("version").GetString());
    }
    [Fact]
    public async Task Large_valid_metadata_audits_do_not_block_lifecycle_coverage()
    {
        var a=new V2HttpTests.Authority(); await using var h=new V2HttpTests.Host(a); using var c=h.Client(a.User); var id=await Create(c,a);
        foreach(var step in new[]{(1,'ế'),(2,'ồ')}) await V2HttpTests.Data(await c.PutAsJsonAsync($"/api/v2/documents/{id}",new {
            expectedVersion=step.Item1,companyCode="HL",subject=new string(step.Item2,2000),remark=new string(step.Item2,4000),originatorUserId=a.User,ownerDepartmentId=a.Department,sensitivity="Normal"}));
        using(var scope=h.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>(); Assert.Contains(await db.Set<DocumentEditAudit>().ToListAsync(),x=>x.ChangesJson.Length>65536);}
        var result=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/history")); Assert.Equal(0,result.GetProperty("totalCount").GetInt32()); Assert.Equal("3",result.GetProperty("throughVersion").GetString());
    }
    private sealed class ChangingAuthority:IDocumentV2Authority
    {
        public readonly V2HttpTests.Authority Inner=new(); public int Reads;
        public Task<V2RegistrationAuthority> RegisterAsync(Guid u,V2RegistrationDraft d,CancellationToken ct)=>Inner.RegisterAsync(u,d,ct);
        public Task<V2MutationAuthority> MutateAsync(Guid u,Guid d,V2EditDraft? e,CancellationToken ct)=>Inner.MutateAsync(u,d,e,ct);
        public Task<V2FormAuthority> FormAsync(Guid u,string k,CancellationToken ct)=>Inner.FormAsync(u,k,ct);
        public Task<V2ReadAuthority> ReadAsync(Guid u,CancellationToken ct) {if(++Reads==2)Inner.Readable.Clear(); return Inner.ReadAsync(u,ct);}
    }
}
