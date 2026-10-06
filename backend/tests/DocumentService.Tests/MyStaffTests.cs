using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class MyStaffTests
{
    [Fact] public async Task Scoped_staff_survive_tms_failure_without_broadening_the_task_scope()
    {var a=new Staff();var result=await new MyStaffService(a,new UnavailableTmsConnector()).QueryAsync(a.User,1,20,true,default);Assert.Single(result.Staff);Assert.Null(result.Tasks);Assert.Equal("TMS_UNAVAILABLE",result.TaskState);}
    [Fact] public async Task Foreign_task_is_discarded_and_authority_mismatch_fails_closed()
    {var a=new Staff();var result=await new MyStaffService(a,new Tms()).QueryAsync(a.User,1,20,true,default);Assert.Null(result.Tasks);Assert.Equal("TMS_RESPONSE_INVALID",result.TaskState);var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new MyStaffService(a,new Tms()).QueryAsync(Guid.NewGuid(),1,20,true,default));Assert.Equal(503,e.Status);}
    [Fact] public async Task Task_timeout_is_reconciled_by_correlation_without_a_second_create_or_a_DAS_grant()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register("INTERNAL");var read=new V2HttpTests.Authority();read.Readable.Add(doc.Id);var staff=new Staff(read.User);var tms=new Tms{Timeout=true};
        var service=new DocumentTasks(f.Db,read,staff,tms,TimeProvider.System);var draft=new DocumentTaskDraft(staff.Member,"Task");
        var first=await service.CreateAsync(read.User,doc.Id,"request",draft,default);Assert.Equal("UnknownOutcome",first.State);
        var replay=await service.CreateAsync(read.User,doc.Id,"request",draft,default);Assert.Equal(first.CorrelationId,replay.CorrelationId);Assert.Equal(1,tms.Calls);
        tms.Timeout=false;var resolved=await service.ReconcileAsync(read.User,first.CorrelationId,default);Assert.Equal("Linked",resolved.State);Assert.Equal("task-test",resolved.TaskId);Assert.Equal(1,tms.Calls);Assert.Single(read.Readable);
        read.Readable.Clear();var denied=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.CreateAsync(read.User,doc.Id,"different",draft,default));Assert.Equal(404,denied.Status);
    }
    [Fact] public async Task Missing_tms_preserves_intent_but_changed_key_body_conflicts()
    {await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register("INTERNAL");var read=new V2HttpTests.Authority();read.Readable.Add(doc.Id);var staff=new Staff(read.User);var service=new DocumentTasks(f.Db,read,staff,new UnavailableTmsConnector(),TimeProvider.System);var r=await service.CreateAsync(read.User,doc.Id,"pending",new(staff.Member,"Title"),default);Assert.Equal("PendingConfiguration",r.State);Assert.Single(await f.Db.Set<DocumentTaskIntent>().ToListAsync());var conflict=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.CreateAsync(read.User,doc.Id,"pending",new(staff.Member,"Other"),default));Assert.Equal(409,conflict.Status);}
    private sealed class Staff(Guid? user=null):IStaffAuthority
    {public Guid User=user??Guid.NewGuid(),Member=Guid.NewGuid();public Task<StaffAuthority> ResolveAsync(Guid u,CancellationToken ct)=>Task.FromResult(new StaffAuthority(User,true,[new(Member,"Member",Guid.NewGuid(),"IT")]));}
    private sealed class Tms:ITmsConnector
    {public bool Timeout;public int Calls;
        public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> u,int p,int s,CancellationToken ct)=>Task.FromResult(new StaffTaskPage([new("foreign",Guid.NewGuid(),"Foreign","Open",null)],1,p,s));
        public Task<TmsCreateResult> CreateAsync(Guid c,Guid d,Guid a,Guid assignee,string title,CancellationToken ct){Calls++;if(Timeout)throw new HttpRequestException("Lost response");return Task.FromResult(new TmsCreateResult("Linked","task-test"));}
        public Task<TmsCreateResult> ReconcileAsync(Guid c,CancellationToken ct)=>Task.FromResult(new TmsCreateResult("Linked","task-test"));}
}
