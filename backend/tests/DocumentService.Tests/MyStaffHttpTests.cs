using System.Net;
using Xunit;

namespace DocumentService.Tests;

// HTTP boundary with synthetic trusted scope and an in-process TMS adapter.
// This does not exercise the real EAP hierarchy or the real TMS API.
public sealed class MyStaffHttpTests
{
    [Fact]
    public async Task Http_staff_and_task_pages_use_trusted_scope_without_granting_document_read()
    {
        var authority = new Authority();
        var tms = new Tms(authority);
        await using var host = new V2HttpTests.Host(staffAuthority: authority, tms: tms);
        using var client = host.Client(authority.User);
        var data = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff?pageNumber=2&pageSize=1"));
        Assert.Equal(2, data.GetProperty("total").GetInt32());
        Assert.Equal(authority.Bob, data.GetProperty("staff")[0].GetProperty("userId").GetGuid());
        Assert.Equal("Connected", data.GetProperty("taskState").GetString());
        Assert.Equal(authority.Alice, data.GetProperty("tasks").GetProperty("items")[0].GetProperty("assigneeUserId").GetGuid());
        Assert.Equal(2, tms.RequestedPage);
        Assert.Equal(1, tms.RequestedSize);
        Assert.True(tms.RequestedUsers!.SetEquals([authority.Alice, authority.Bob]));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v2/documents?kind=Internal")).StatusCode);
    }

    [Fact]
    public async Task Http_foreign_task_response_is_rejected_while_staff_remain_visible()
    {
        var authority = new Authority();
        await using var host = new V2HttpTests.Host(staffAuthority: authority, tms: new Tms(authority) { Foreign = true });
        using var client = host.Client(authority.User);
        var data = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff"));
        Assert.Equal(2, data.GetProperty("staff").GetArrayLength());
        Assert.Equal("TMS_RESPONSE_INVALID", data.GetProperty("taskState").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, data.GetProperty("tasks").ValueKind);
    }

    [Fact]
    public async Task Http_staff_without_tasks_does_not_call_tms_and_actor_mismatch_fails_closed()
    {
        var authority = new Authority();
        var tms = new Tms(authority);
        await using var host = new V2HttpTests.Host(staffAuthority: authority, tms: tms);
        using var client = host.Client(authority.User);
        var data = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff?includeTasks=false"));
        Assert.Equal("NotRequested", data.GetProperty("taskState").GetString());
        Assert.Null(tms.RequestedUsers);
        using var stranger = host.Client(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await stranger.GetAsync("/api/v2/my-staff")).StatusCode);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=1&pageNumber=2")]
    [InlineData("userId=11111111-1111-4111-8111-111111111111")]
    public async Task Http_invalid_paging_or_caller_scope_cannot_reach_tms(string query)
    {
        var authority = new Authority();
        var tms = new Tms(authority);
        await using var host = new V2HttpTests.Host(staffAuthority: authority, tms: tms);
        using var client = host.Client(authority.User);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v2/my-staff?" + query)).StatusCode);
        Assert.Null(tms.RequestedUsers);
    }

    private sealed class Authority : IStaffAuthority
    {
        public Guid User = Guid.NewGuid(), Alice = Guid.NewGuid(), Bob = Guid.NewGuid(), Department = Guid.NewGuid();
        public Task<StaffAuthority> ResolveAsync(Guid user, CancellationToken ct) => Task.FromResult(new StaffAuthority(User, true,
            [new(Bob, "Bob fixture", Department, "IT"), new(Alice, "Alice fixture", Department, "IT")]));
    }

    private sealed class Tms(Authority authority) : ITmsConnector
    {
        public bool Foreign;
        public HashSet<Guid>? RequestedUsers;
        public int RequestedPage, RequestedSize;
        public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users, int page, int size, CancellationToken ct)
        {
            RequestedUsers = users.ToHashSet(); RequestedPage = page; RequestedSize = size;
            return Task.FromResult(new StaffTaskPage([new("fixture-task", Foreign ? Guid.NewGuid() : authority.Alice, "Fixture task", "Pending", null)], 2, page, size));
        }
        public Task<TmsCreateResult> CreateAsync(Guid correlation, Guid document, Guid actor, Guid assignee, string title, CancellationToken ct) => throw new NotSupportedException();
        public Task<TmsCreateResult> ReconcileAsync(Guid correlation, CancellationToken ct) => throw new NotSupportedException();
    }
}
