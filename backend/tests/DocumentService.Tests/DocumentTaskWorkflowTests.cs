using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace DocumentService.Tests;

public sealed class DocumentTaskWorkflowTests
{
    [Fact] public async Task Options_are_document_scoped_and_only_self_and_managed_staff()
    {
        var a=new V2HttpTests.Authority();var staff=new Staff(a.User);await using var h=new V2HttpTests.Host(a,staffAuthority:staff);using var c=h.Client(a.User);
        var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),"doc")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{id}/tasks/options")).StatusCode);a.Readable.Add(id);
        var data=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/tasks/options"));Assert.Equal(a.User,data.GetProperty("userId").GetGuid());
        var ids=data.GetProperty("assignees").EnumerateArray().Select(x=>x.GetProperty("userId").GetGuid()).ToHashSet();Assert.Equal(2,ids.Count);Assert.Contains(a.User,ids);Assert.Contains(staff.Member,ids);
        staff.Active=false;Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/v2/documents/{id}/tasks/options")).StatusCode);
    }
    [Fact] public async Task Owned_history_and_retry_survive_new_request_scopes_without_new_correlation()
    {
        var a=new V2HttpTests.Authority();var staff=new Staff(a.User);var tms=new Tms();await using var h=new V2HttpTests.Host(a,staffAuthority:staff,tms:tms);using var c=h.Client(a.User);
        var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),"doc")).GetProperty("id").GetGuid();a.Readable.Add(id);
        c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.Add("Idempotency-Key","task-key");var first=await Receipt(await c.PostAsJsonAsync($"/api/v2/documents/{id}/tasks",new{assigneeUserId=staff.Member,title="Frozen title"}));var correlation=first.GetProperty("correlationId").GetGuid();
        using var other=h.Client(Guid.NewGuid());var foreign=await V2HttpTests.Data(await other.GetAsync($"/api/v2/documents/{id}/tasks"));Assert.Equal(0,foreign.GetProperty("total").GetInt32());Assert.Equal(HttpStatusCode.NotFound,(await other.PostAsync($"/api/v2/task-intents/{correlation}/retry",null)).StatusCode);
        var history=await V2HttpTests.Data(await c.GetAsync($"/api/v2/documents/{id}/tasks"));Assert.True(history.GetProperty("hasPending").GetBoolean());Assert.Equal("Frozen title",history.GetProperty("items")[0].GetProperty("title").GetString());Assert.DoesNotContain("keyHash",history.ToString());
        tms.Connected=true;var linked=await V2HttpTests.Data(await c.PostAsync($"/api/v2/task-intents/{correlation}/retry",null));Assert.Equal(correlation,linked.GetProperty("correlationId").GetGuid());Assert.Equal("Linked",linked.GetProperty("state").GetString());Assert.Equal(2,tms.Calls);Assert.Equal("Frozen title",tms.Title);
        await c.PostAsync($"/api/v2/task-intents/{correlation}/retry",null);Assert.Equal(2,tms.Calls);a.Readable.Clear();Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{id}/tasks")).StatusCode);
    }
    [Fact] public async Task Unknown_retry_does_not_create_again_and_reconcile_is_the_only_remote_operation()
    {
        var a=new V2HttpTests.Authority();var staff=new Staff(a.User);var tms=new Tms{LoseReply=true};await using var h=new V2HttpTests.Host(a,staffAuthority:staff,tms:tms);using var c=h.Client(a.User);
        var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),"doc")).GetProperty("id").GetGuid();a.Readable.Add(id);c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.Add("Idempotency-Key","lost");var first=await Receipt(await c.PostAsJsonAsync($"/api/v2/documents/{id}/tasks",new{assigneeUserId=staff.Member,title="Lost"}));var intent=first.GetProperty("correlationId").GetGuid();
        var replay=await Receipt(await c.PostAsync($"/api/v2/task-intents/{intent}/retry",null));Assert.Equal("UnknownOutcome",replay.GetProperty("state").GetString());Assert.Equal(1,tms.Calls);
        var linked=await V2HttpTests.Data(await c.PostAsync($"/api/v2/task-intents/{intent}/reconcile",null));Assert.Equal("remote-task",linked.GetProperty("taskId").GetString());Assert.Equal(1,tms.Calls);Assert.Equal(1,tms.Reconciles);
    }
    [Theory][InlineData("duplicate")][InlineData("empty")][InlineData("mismatch")]
    public async Task Corrupt_staff_projection_blocks_options_and_create(string mode)
    {
        var a=new V2HttpTests.Authority();var staff=new Staff(a.User){Mode=mode};await using var h=new V2HttpTests.Host(a,staffAuthority:staff);using var c=h.Client(a.User);var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),"doc")).GetProperty("id").GetGuid();a.Readable.Add(id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync($"/api/v2/documents/{id}/tasks/options")).StatusCode);Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.PostAsJsonAsync($"/api/v2/documents/{id}/tasks",new{assigneeUserId=staff.Member,title="No grant"})).StatusCode);
    }
    [Fact] public async Task Anonymous_history_and_options_are_rejected()
    {await using var h=new V2HttpTests.Host();using var c=h.CreateClient();foreach(var suffix in new[]{"", "/options"})Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync($"/api/v2/documents/{Guid.NewGuid()}/tasks{suffix}")).StatusCode);}
    [Fact] public async Task Revoked_assignee_cannot_retry_and_unexpected_query_cannot_override_actor()
    {
        var a=new V2HttpTests.Authority();var staff=new Staff(a.User);var tms=new Tms();await using var h=new V2HttpTests.Host(a,staffAuthority:staff,tms:tms);using var c=h.Client(a.User);
        var id=(await V2HttpTests.Register(c,V2HttpTests.Draft(a,"Internal"),"doc")).GetProperty("id").GetGuid();a.Readable.Add(id);c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.Add("Idempotency-Key","revoked");var first=await Receipt(await c.PostAsJsonAsync($"/api/v2/documents/{id}/tasks",new{assigneeUserId=staff.Member,title="Pending"}));
        staff.Mode="none";var correlation=first.GetProperty("correlationId").GetGuid();Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v2/task-intents/{correlation}/retry",null)).StatusCode);Assert.Equal(1,tms.Calls);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"/api/v2/documents/{id}/tasks?actorUserId={Guid.NewGuid()}")).StatusCode);
    }
    private static async Task<JsonElement> Receipt(HttpResponseMessage r){Assert.Equal(HttpStatusCode.Accepted,r.StatusCode);Assert.Equal("no-store",r.Headers.CacheControl?.ToString());using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return j.RootElement.GetProperty("data").Clone();}
    private sealed class Staff(Guid user):IStaffAuthority
    {public Guid Member=Guid.NewGuid();public bool Active=true;public string Mode="";public Task<StaffAuthority> ResolveAsync(Guid u,CancellationToken ct){var member=new StaffMember(Mode=="empty"?Guid.Empty:Member,"Member",Guid.NewGuid(),"IT");return Task.FromResult(new StaffAuthority(Mode=="mismatch"?Guid.NewGuid():user,Active,Mode=="duplicate"?[member,member]:Mode=="none"?[]:[member]));}}
    private sealed class Tms:ITmsConnector
    {public bool Connected,LoseReply;public int Calls,Reconciles;public string? Title;public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> u,int p,int s,CancellationToken ct)=>throw new NotSupportedException();
        public Task<TmsCreateResult> CreateAsync(Guid correlation,Guid d,Guid a,Guid assignee,string title,CancellationToken ct){Calls++;Title=title;if(LoseReply)throw new HttpRequestException("Lost reply");return Task.FromResult(Connected?new TmsCreateResult("Linked","remote-task"):new("PendingConfiguration"));}
        public Task<TmsCreateResult> ReconcileAsync(Guid correlation,CancellationToken ct){Reconciles++;return Task.FromResult(new TmsCreateResult("Linked","remote-task"));}}
}
