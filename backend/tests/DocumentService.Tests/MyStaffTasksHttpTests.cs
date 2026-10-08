using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DocumentService.Tests;

// Real controller/service/SQLite host; only unavailable external authorities are fixtures.
public sealed class MyStaffTasksHttpTests
{
    [Fact]
    public async Task Assignee_outside_staff_page_filters_before_task_paging_and_maps_full_fresh_scope()
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms);
        using var client = host.Client(a.User);
        var staff = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff?pageNumber=1&pageSize=1&includeTasks=false"));
        Assert.Equal(a.Alice, staff.GetProperty("staff")[0].GetProperty("userId").GetGuid());
        var old = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff?pageNumber=1&pageSize=1"));
        Assert.Equal("alice-1", old.GetProperty("tasks").GetProperty("items")[0].GetProperty("taskId").GetString());
        tms.BeforeReturn = () => a.BobName = "Bob refreshed";
        var data = await V2HttpTests.Data(await client.GetAsync($"/api/v2/my-staff/tasks?pageNumber=1&pageSize=1&assigneeUserId={a.Bob}"));
        Assert.Equal("Bob refreshed", data.GetProperty("selectedAssignee").GetProperty("name").GetString());
        var tasks = data.GetProperty("tasks"); Assert.Equal(1, tasks.GetProperty("total").GetInt32());
        var item = tasks.GetProperty("items")[0]; Assert.Equal("bob-1", item.GetProperty("taskId").GetString());
        Assert.Equal(a.Bob, item.GetProperty("assignee").GetProperty("userId").GetGuid());
        Assert.Equal("Bob refreshed", item.GetProperty("assignee").GetProperty("name").GetString());
        Assert.True(tms.Users!.SetEquals([a.Bob]));
    }

    [Fact]
    public async Task Unfiltered_task_page_is_independent_and_uses_verified_names_and_utc_dates()
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        var data = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff/tasks?pageNumber=3&pageSize=1"));
        Assert.Equal(JsonValueKind.Null, data.GetProperty("selectedAssignee").ValueKind);
        var tasks = data.GetProperty("tasks"); Assert.Equal(3, tasks.GetProperty("total").GetInt32());
        Assert.Equal(3, tasks.GetProperty("pageNumber").GetInt32());
        var item = tasks.GetProperty("items")[0]; Assert.Equal("Bob fixture", item.GetProperty("assignee").GetProperty("name").GetString());
        Assert.Equal("OpaqueStatus", item.GetProperty("status").GetString());
        Assert.Equal("2026-10-08T00:00:00Z", item.GetProperty("dueAt").GetString());
    }

    [Theory]
    [InlineData("pageNumber=0")][InlineData("pageNumber=1000001")][InlineData("pageSize=101")]
    [InlineData("pageSize=")][InlineData("pageNumber=abc")][InlineData("pageNumber=1&pageNumber=2")]
    [InlineData("assigneeUserId=")][InlineData("assigneeUserId=00000000-0000-0000-0000-000000000000")]
    [InlineData("assigneeUserId=bad")][InlineData("assigneeUserId=11111111111141118111111111111111")]
    [InlineData("assigneeUserId=%2011111111-1111-4111-8111-111111111111%20")]
    [InlineData("managerId=11111111-1111-4111-8111-111111111111")][InlineData("departmentId=1")]
    [InlineData("userIds=1")][InlineData("status=Open")][InlineData("includeTasks=true")]
    public async Task Invalid_queries_cannot_call_authority_or_tms(string query)
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        await Error(await client.GetAsync("/api/v2/my-staff/tasks?" + query), HttpStatusCode.BadRequest, "INVALID_STAFF_QUERY");
        Assert.Equal(0, a.Calls); Assert.Equal(0, tms.Calls);
    }

    [Fact]
    public async Task Assignee_outside_scope_is_forbidden_without_connector_or_name_disclosure()
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        await Error(await client.GetAsync($"/api/v2/my-staff/tasks?assigneeUserId={Guid.NewGuid()}"), HttpStatusCode.Forbidden, "ASSIGNEE_FORBIDDEN");
        Assert.Equal(0, tms.Calls);
    }

    [Fact]
    public async Task Verified_empty_scope_returns_connected_empty_page_without_tms()
    {
        var a = new Authority { Empty = true }; var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        var data = await V2HttpTests.Data(await client.GetAsync("/api/v2/my-staff/tasks?pageNumber=3&pageSize=7"));
        Assert.Equal("Connected", data.GetProperty("taskState").GetString());
        Assert.Equal(0, data.GetProperty("tasks").GetProperty("total").GetInt32());
        Assert.Empty(data.GetProperty("tasks").GetProperty("items").EnumerateArray()); Assert.Equal(0, tms.Calls);
    }

    [Theory]
    [InlineData("wrongPage")][InlineData("wrongSize")][InlineData("negativeTotal")][InlineData("smallTotal")]
    [InlineData("tooMany")][InlineData("pastEnd")][InlineData("terminalOverflow")][InlineData("duplicate")][InlineData("foreign")]
    [InlineData("unselected")][InlineData("emptyId")][InlineData("longId")][InlineData("emptyTitle")]
    [InlineData("longTitle")][InlineData("emptyStatus")][InlineData("longStatus")][InlineData("nonUtc")]
    [InlineData("nullPage")][InlineData("nullItems")][InlineData("nullItem")]
    public async Task Entire_malformed_tms_page_is_discarded(string mode)
    {
        var a = new Authority(); var tms = new Tms(a) { Mode = mode };
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        var page = mode == "pastEnd" ? 3 : mode == "terminalOverflow" ? 2 : 1;
        var data = await V2HttpTests.Data(await client.GetAsync($"/api/v2/my-staff/tasks?pageNumber={page}&pageSize=2&assigneeUserId={a.Bob}"));
        Assert.Equal("TMS_CONTRACT_INVALID", data.GetProperty("taskState").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("tasks").ValueKind); Assert.Equal(2, a.Calls);
    }

    [Theory]
    [InlineData("unavailable", "TMS_UNAVAILABLE")][InlineData("timeout", "TMS_TIMEOUT")]
    [InlineData("other503", "TMS_UNAVAILABLE")][InlineData("json", "TMS_CONTRACT_INVALID")]
    public async Task Tms_failures_return_explicit_degraded_state_and_revalidate_authority(string mode, string expected)
    {
        var a = new Authority(); var tms = new Tms(a) { Mode = mode };
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        var data = await V2HttpTests.Data(await client.GetAsync($"/api/v2/my-staff/tasks?assigneeUserId={a.Bob}"));
        Assert.Equal(expected, data.GetProperty("taskState").GetString()); Assert.Equal(JsonValueKind.Null, data.GetProperty("tasks").ValueKind);
        Assert.Equal(a.Bob, data.GetProperty("selectedAssignee").GetProperty("userId").GetGuid()); Assert.Equal(2, a.Calls);
    }

    [Theory]
    [InlineData("inactive", 403, "ACTOR_INACTIVE")][InlineData("offline", 503, "STAFF_AUTHORITY_UNAVAILABLE")]
    [InlineData("mismatch", 503, "AUTHORITY_MISMATCH")][InlineData("changed", 409, "STAFF_SCOPE_CHANGED")]
    public async Task Fresh_authority_revocation_discards_old_task_payload(string mode, int status, string code)
    {
        var a = new Authority(); var tms = new Tms(a) { BeforeReturn = () => a.Mode = mode };
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        await Error(await client.GetAsync("/api/v2/my-staff/tasks"), (HttpStatusCode)status, code); Assert.Equal(2, a.Calls);
    }

    [Theory]
    [InlineData("inactive", 403, "ACTOR_INACTIVE")][InlineData("offline", 503, "STAFF_AUTHORITY_UNAVAILABLE")]
    [InlineData("mismatch", 503, "AUTHORITY_MISMATCH")][InlineData("duplicate", 503, "AUTHORITY_MISMATCH")]
    public async Task Initial_authority_invalidity_cannot_reach_tms(string mode, int status, string code)
    {
        var a = new Authority { Mode = mode }; var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        await Error(await client.GetAsync("/api/v2/my-staff/tasks"), (HttpStatusCode)status, code); Assert.Equal(0, tms.Calls);
    }

    [Fact]
    public async Task Expired_jwt_cannot_resolve_scope_or_tms()
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.CreateClient();
        var token = new JwtSecurityToken(claims: [new("sub", a.User.ToString()), new(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddMinutes(-10), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(V2HttpTests.Host.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        var response = await client.GetAsync("/api/v2/my-staff/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(0, a.Calls); Assert.Equal(0, tms.Calls);
    }

    [Theory]
    [InlineData("/api/v2/my-staff/tasks")][InlineData("/api/v2/my-staff")]
    public async Task Missing_jwt_cannot_cache_private_api_challenge(string path)
    {
        var a = new Authority(); var tms = new Tms(a);
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(0, a.Calls); Assert.Equal(0, tms.Calls);
    }

    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean()); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Request_cancellation_reaches_connector_and_is_not_returned_as_degraded_timeout()
    {
        var a = new Authority(); var tms = new WaitingTms();
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        using var canceled = new CancellationTokenSource();
        var request = client.GetAsync("/api/v2/my-staff/tasks", canceled.Token);
        await Task.WhenAny(request, tms.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(tms.Started.Task.IsCompletedSuccessfully);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await tms.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, a.Calls);
    }

    [Fact]
    public async Task Tms_deadline_cancels_connector_and_returns_timeout_with_unknown_task_count()
    {
        var a = new Authority(); var tms = new WaitingTms();
        await using var host = new V2HttpTests.Host(staffAuthority: a, tms: tms); using var client = host.Client(a.User);
        var request = client.GetAsync("/api/v2/my-staff/tasks");
        var data = await V2HttpTests.Data(await request.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal("TMS_TIMEOUT", data.GetProperty("taskState").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("tasks").ValueKind);
        await tms.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(1)); Assert.Equal(2, a.Calls);
    }

    private sealed class WaitingTms : ITmsConnector
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users, int page, int size, CancellationToken ct)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { Canceled.TrySetResult(); throw; }
            throw new InvalidOperationException();
        }
        public Task<TmsCreateResult> CreateAsync(Guid correlation, Guid document, Guid actor, Guid assignee, string title, CancellationToken ct) => throw new NotSupportedException();
        public Task<TmsCreateResult> ReconcileAsync(Guid correlation, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class Authority : IStaffAuthority
    {
        internal readonly Guid User = Guid.NewGuid(), Alice = Guid.NewGuid(), Bob = Guid.NewGuid(), Department = Guid.NewGuid();
        internal bool Empty; internal string Mode = "", BobName = "Bob fixture"; internal int Calls;
        public Task<StaffAuthority> ResolveAsync(Guid user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            if (Mode == "offline") throw new DocumentRegistrationRuleException(503, "STAFF_AUTHORITY_UNAVAILABLE", "Fixture unavailable");
            StaffMember[] members = Empty ? [] : [new(Alice, "Alice fixture", Department, "IT"), new(Bob, BobName, Department, "IT")];
            if (Mode == "changed") members = [members[0]];
            if (Mode == "duplicate") members = [members[0], members[0]];
            return Task.FromResult(new StaffAuthority(Mode == "mismatch" ? Guid.NewGuid() : User, Mode != "inactive", members));
        }
    }

    internal sealed class Tms(Authority a) : ITmsConnector
    {
        internal int Calls; internal HashSet<Guid>? Users; internal string Mode = ""; internal Action? BeforeReturn;
        public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users, int page, int size, CancellationToken ct)
        {
            Calls++; Users = users.ToHashSet(); BeforeReturn?.Invoke();
            if (Mode == "unavailable") throw new HttpRequestException("Fixture unavailable");
            if (Mode == "timeout") throw new OperationCanceledException();
            if (Mode == "other503") throw new DocumentRegistrationRuleException(503, "OTHER_TMS_FAILURE", "Fixture");
            if (Mode == "json") throw new JsonException("Fixture malformed");
            var all = new StaffTask[] { new("alice-1", a.Alice, "Alice first", "Pending", null), new("alice-2", a.Alice, "Alice second", "Pending", null), new("bob-1", a.Bob, "Bob third", "OpaqueStatus", new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)) };
            var filtered = all.Where(x => users.Contains(x.AssigneeUserId)).ToArray();
            var items = filtered.Skip((page - 1) * size).Take(size).ToArray(); var total = filtered.Length;
            var task = all[2];
            switch (Mode)
            {
                case "wrongPage": page++; break;
                case "wrongSize": size++; break;
                case "negativeTotal": total = -1; break;
                case "smallTotal": total = 0; break;
                case "tooMany": items = [task, task with { TaskId = "second" }, task with { TaskId = "third" }]; total = 3; break;
                case "pastEnd": items = [task]; total = 1; break;
                case "terminalOverflow": items = [task, task with { TaskId = "second" }]; total = 3; break;
                case "duplicate": items = [task, task]; total = 2; break;
                case "foreign": items = [task, task with { TaskId = "foreign", AssigneeUserId = Guid.NewGuid() }]; total = 2; break;
                case "unselected": items = [task, all[0]]; total = 2; break;
                case "emptyId": items = [task with { TaskId = " " }]; break;
                case "longId": items = [task with { TaskId = new('x', 201) }]; break;
                case "emptyTitle": items = [task with { Title = " " }]; break;
                case "longTitle": items = [task with { Title = new('x', 2001) }]; break;
                case "emptyStatus": items = [task with { Status = " " }]; break;
                case "longStatus": items = [task with { Status = new('x', 101) }]; break;
                case "nonUtc": items = [task with { DueAt = new DateTimeOffset(2026, 10, 8, 7, 0, 0, TimeSpan.FromHours(7)) }]; break;
                case "nullPage": return Task.FromResult<StaffTaskPage>(null!);
                case "nullItems": return Task.FromResult(new StaffTaskPage(null!, total, page, size));
                case "nullItem": items = [null!]; break;
            }
            return Task.FromResult(new StaffTaskPage(items, total, page, size));
        }
        public Task<TmsCreateResult> CreateAsync(Guid correlation, Guid document, Guid actor, Guid assignee, string title, CancellationToken ct) => throw new NotSupportedException();
        public Task<TmsCreateResult> ReconcileAsync(Guid correlation, CancellationToken ct) => throw new NotSupportedException();
    }
}
