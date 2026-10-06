using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2LifecycleSqlTests
{
    [CatalogSqlFact]
    public async Task Parallel_different_transitions_with_same_version_have_one_winner()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create(); Document d;
        await using(var db=f.Db()) d=await V2LifecycleTests.Register(db);
        async Task<int> Run(V2StatusAction action) {
            await using var db=f.Db(); try { await V2LifecycleTests.Change(db,d,action,action==V2StatusAction.Cancel?"Reason":null); return 200; }
            catch(DocumentRegistrationRuleException e) { return e.Status; } }
        var results=await Task.WhenAll(Enumerable.Range(0,16).Select(i=>Run(i%2==0?V2StatusAction.Cancel:V2StatusAction.Distribute)));
        Assert.Equal(1,results.Count(x=>x==200)); Assert.Equal(15,results.Count(x=>x==409));
        await using var check=f.Db(); Assert.Equal(2,(await check.DocumentRegistrations.SingleAsync()).Version);
        Assert.Single(await check.Set<DocumentEditAudit>().ToListAsync()); Assert.Equal(2,await check.DocumentStatusHistory.CountAsync());
        Assert.Equal(2,await check.DocumentOutboxEvents.CountAsync()); Assert.Equal(1,(await check.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Cancellation_and_header_edit_share_aggregate_cas_and_preserve_winner()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create(); Document d;
        await using(var db=f.Db()) d=await V2LifecycleTests.Register(db);
        async Task<int> Status() { await using var db=f.Db(); try { await V2LifecycleTests.Change(db,d,V2StatusAction.Cancel,"Reason"); return 200; } catch(DocumentRegistrationRuleException e) { return e.Status; } }
        async Task<int> Edit() { await using var db=f.Db(); try { await new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(d.Id,V2KindDetailsTests.Header(d,null) with{Subject="Edit winner"},V2EditingTests.Actor()); return 200; } catch(DocumentRegistrationRuleException e) { return e.Status; } }
        var status=Status(); var edit=Edit(); await Task.WhenAll(status,edit); Assert.Equal(1,new[]{status.Result,edit.Result}.Count(x=>x==200));
        await using var check=f.Db(); var saved=await check.Documents.SingleAsync(); Assert.Equal(status.Result==200?"Cancelled":"InProgress",saved.Status);
        Assert.Equal(edit.Result==200?"Edit winner":"Registered subject",saved.Title); Assert.Single(await check.Set<DocumentEditAudit>().ToListAsync());
    }

    [CatalogSqlFact]
    public async Task Real_audit_constraint_failure_rolls_back_cancellation_and_status_history()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create(); await using var db=f.Db(); var d=await V2LifecycleTests.Register(db);
        db.Set<DocumentEditAudit>().Add(new(){DocumentId=d.Id,Version=2,ActorUserId=V2EditingTests.Actor().UserId,ChangesJson="{}",ChangedAt=V2EditingTests.Clock.GetUtcNow()}); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(()=>V2LifecycleTests.Change(db,d,V2StatusAction.Cancel,"Reason"));
        Assert.Equal("InProgress",(await db.Documents.SingleAsync()).Status); Assert.Equal(1,(await db.DocumentRegistrations.SingleAsync()).Version);
        Assert.Empty(await db.Set<DocumentCancellation>().ToListAsync()); Assert.Single(await db.DocumentStatusHistory.ToListAsync()); Assert.Single(await db.DocumentOutboxEvents.ToListAsync());
    }

    [CatalogSqlFact]
    public async Task Additive_migration_keeps_relation_metadata_numbers_receipts_and_historical_cancelled_data()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create("20261004132429_AddDocumentRelations"); await using var db=f.Db();
        var outgoing=await V2LifecycleTests.Register(db,"OUTGOING"); var incoming=await V2RelationTests.Register(db,"INCOMING");
        outgoing=await new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(outgoing.Id,V2RelationTests.Edit(outgoing,new([incoming.Id])),V2EditingTests.Actor(),relationScope:V2RelationTests.Scope(outgoing.Id,incoming.Id));
        var hash=(await db.RegistrationRequests.SingleAsync(x=>x.DocumentId==outgoing.Id)).BodyHash;
        incoming.Status="Cancelled"; await db.SaveChangesAsync(); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        incoming=await db.Documents.Include(x=>x.Registration).SingleAsync(x=>x.Id==incoming.Id);
        Assert.Single(await db.Set<DocumentRelation>().ToListAsync()); Assert.Single(await db.Set<DocumentRecipient>().ToListAsync()); Assert.Empty(await db.Set<DocumentCancellation>().ToListAsync());
        Assert.Equal(hash,(await db.RegistrationRequests.SingleAsync(x=>x.DocumentId==outgoing.Id)).BodyHash); Assert.Equal("27-01-0001/HL/ADM",(await db.Documents.SingleAsync(x=>x.Id==outgoing.Id)).DocumentNumber);
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>V2LifecycleTests.Change(db,incoming,V2StatusAction.Restore)); Assert.Equal(409,e.Status);
        outgoing=await V2LifecycleTests.Change(db,outgoing,V2StatusAction.Distribute); outgoing=await V2LifecycleTests.Change(db,outgoing,V2StatusAction.Cancel,"After upgrade");
        outgoing=await V2LifecycleTests.Change(db,outgoing,V2StatusAction.Restore); Assert.Equal("Distributed",outgoing.Status);
        Assert.Single(await db.Set<DocumentRelation>().ToListAsync()); Assert.Equal(2,(await db.DocumentNumberCounters.ToListAsync()).Sum(x=>x.CurrentValue));
    }

    [CatalogSqlFact]
    public async Task Cancellation_table_enforces_previous_state_reason_and_restore_pair()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create(); await using var db=f.Db(); var d=await V2LifecycleTests.Register(db);
        foreach(var invalid in new[]{"state","reason","restore"}) {
            db.Set<DocumentCancellation>().Add(new(){DocumentId=d.Id,PreviousStatus=invalid=="state"?"Reviewed":"InProgress",Reason=invalid=="reason"?"   ":"Valid",
                CancelledAt=V2EditingTests.Clock.GetUtcNow(),CancelledByUserId=V2EditingTests.Actor().UserId,RestoredByUserId=invalid=="restore"?V2EditingTests.Actor().UserId:null});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); db.ChangeTracker.Clear(); }
        Assert.Empty(await db.Set<DocumentCancellation>().ToListAsync());
    }
}
