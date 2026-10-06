using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class DocumentTaskWorkflowSqlTests
{
    [CatalogSqlFact] public async Task Concurrent_retry_claims_one_remote_create_and_preserves_the_existing_correlation()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();var read=new V2HttpTests.Authority();var staff=new Staff(read.User);var remote=new Connector();Document doc;DocumentTaskReceipt pending;
        await using(var db=f.Db()) {doc=await V2LifecycleTests.Register(db);read.Readable.Add(doc.Id);pending=await new DocumentTasks(db,read,staff,remote,TimeProvider.System).CreateAsync(read.User,doc.Id,"retry-sql",new(staff.Member,"Frozen"),default);}
        remote.Connected=true;
        await using var a=f.Db();await using var b=f.Db();var first=new DocumentTasks(a,read,staff,remote,TimeProvider.System).RetryAsync(read.User,pending.CorrelationId,default);
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try {var second=await new DocumentTasks(b,read,staff,remote,TimeProvider.System).RetryAsync(read.User,pending.CorrelationId,default);Assert.Equal(pending.CorrelationId,second.CorrelationId);Assert.Equal("Preparing",second.State);Assert.Equal(2,remote.Calls);}
        finally {remote.Release.TrySetResult();}
        var linked=await first;Assert.Equal("Linked",linked.State);Assert.Equal(pending.CorrelationId,linked.CorrelationId);
        await using var verify=f.Db();Assert.Single(await verify.Set<DocumentTaskIntent>().ToListAsync());Assert.Equal(2,remote.Calls);Assert.Equal("Frozen",remote.Title);
        var history=await new DocumentTasks(verify,read,staff,remote,TimeProvider.System).HistoryAsync(read.User,doc.Id,default);Assert.False(history.HasPending);Assert.Single(history.Items);Assert.Equal("Linked",history.Items[0].State);
    }
    private sealed class Staff(Guid user):IStaffAuthority
    {public Guid Member=Guid.NewGuid();public Task<StaffAuthority> ResolveAsync(Guid u,CancellationToken ct)=>Task.FromResult(new StaffAuthority(user,true,[new(Member,"Staff",Guid.NewGuid(),"IT")]));}
    private sealed class Connector:ITmsConnector
    {
        public bool Connected;public int Calls;public string? Title;public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> u,int p,int s,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<TmsCreateResult> CreateAsync(Guid c,Guid d,Guid a,Guid member,string title,CancellationToken ct){Interlocked.Increment(ref Calls);Title=title;if(!Connected)return new("PendingConfiguration");Entered.TrySetResult();await Release.Task.WaitAsync(ct);return new("Linked","remote-sql");}
        public Task<TmsCreateResult> ReconcileAsync(Guid c,CancellationToken ct)=>throw new NotSupportedException();
    }
}
