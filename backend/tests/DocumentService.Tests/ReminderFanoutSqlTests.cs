using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReminderFanoutSqlTests
{
    [CatalogSqlFact] public async Task Concurrent_manifest_and_dispatch_commit_one_fanout_and_one_receipt_per_inputter()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();var plan=ReminderFanoutTests.Plan();Guid batch;await using(var db=f.Db())batch=(await ReminderDeliveryTests.Batch(db,plan)).Id;var remote=new Receiver();
        var result=await Task.WhenAll(Enumerable.Range(0,10).Select(async _=>{await using var db=f.Db();return await new DurableReminderTransport(db,remote,new ReminderDeliveryTests.Clock(),ReminderDeliveryTests.Config()).EnqueueAsync(batch,plan,default);}));
        await using var verify=f.Db();Assert.Single(await verify.Set<ReminderFanoutManifest>().ToArrayAsync());var row=await verify.Set<ReminderDelivery>().SingleAsync();Assert.Equal("Accepted",row.State);Assert.NotNull(row.NotificationId);Assert.Equal(1,remote.Calls);Assert.Contains("Queued",result);
    }
    [CatalogSqlFact] public async Task Ledger_migration_preserves_existing_numbers_and_task_intents()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create("20261005060101_AddDocumentNotificationDelivery");await using var db=f.Db();var doc=await V2LifecycleTests.Register(db);var original=doc.DocumentNumber;
        db.Add(new DocumentTaskIntent{DocumentId=doc.Id,ActorId=Guid.NewGuid(),AssigneeId=Guid.NewGuid(),Title="Pending",KeyHash=new string('a',64),BodyHash=new string('b',64)});await db.SaveChangesAsync();await db.Database.MigrateAsync();
        Assert.Equal(original,(await db.Documents.SingleAsync()).DocumentNumber);Assert.Single(await db.Set<DocumentTaskIntent>().ToArrayAsync());Assert.Empty(await db.Set<ReminderFanoutManifest>().ToArrayAsync());Assert.Empty(await db.Set<ReminderDelivery>().ToArrayAsync());
    }
    private sealed class Receiver:IReminderNotificationTransport {public int Calls;public async Task<ReminderAcceptance> AcceptAsync(Guid id,ReminderNotificationMessage msg,CancellationToken ct){Interlocked.Increment(ref Calls);await Task.Delay(100,ct);return new("Accepted",Guid.NewGuid(),"Queued");}}
}
