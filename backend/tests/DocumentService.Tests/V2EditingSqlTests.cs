using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2EditingSqlTests
{
    [CatalogSqlFact]
    public async Task Sixteen_editors_of_the_same_version_commit_one_update_audit_and_event()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();Guid id;
        await using(var db=f.Db())id=(await Register(db)).Id;
        var results=await Task.WhenAll(Enumerable.Range(0,16).Select(async i=>{
            await using var db=f.Db();try {await new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(id,V2EditingTests.Draft() with{Subject="Winner "+i},V2EditingTests.Actor(),V2EditingTests.Target());return 200;}
            catch(DocumentRegistrationRuleException e){return e.Status;}
        }));
        Assert.Equal(1,results.Count(x=>x==200));Assert.Equal(15,results.Count(x=>x==409));
        await using var verify=f.Db();Assert.Equal(2,(await verify.DocumentRegistrations.SingleAsync()).Version);
        Assert.Equal("27-01-0001/HV/FIN",(await verify.Documents.SingleAsync()).DocumentNumber);
        Assert.Single(await verify.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(2,await verify.DocumentOutboxEvents.CountAsync());
        Assert.Equal(1,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task SQL_audit_constraint_failure_rolls_back_changed_header_number_and_outbox()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();var doc=await Register(db);
        db.Set<DocumentEditAudit>().Add(new(){DocumentId=doc.Id,ActorUserId=V2EditingTests.Actor().UserId,Version=2,ChangedAt=V2EditingTests.Clock.GetUtcNow(),ChangesJson="{}"});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(()=>new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(doc.Id,V2EditingTests.Draft(),V2EditingTests.Actor(),V2EditingTests.Target()));
        Assert.False(db.ChangeTracker.HasChanges());await using var verify=f.Db();
        Assert.Equal("27-01-0001/HL/ADM",(await verify.Documents.SingleAsync()).DocumentNumber);
        Assert.Equal(1,(await verify.DocumentRegistrations.SingleAsync()).Version);
        Assert.Single(await verify.DocumentOutboxEvents.ToListAsync());Assert.Single(await verify.Set<DocumentEditAudit>().ToListAsync());
    }

    [CatalogSqlFact]
    public async Task Additive_edit_audit_migration_preserves_existing_v2_and_legacy_documents()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create("20261004111636_AddV2RegistrationPersistence");await using var db=f.Db();
        var current=await Register(db);var legacy=new Document{DocType="INTERNAL",Title="Legacy historical",Status="Reviewed",DocumentNumber="20-12-0112/INT/HL/HSE",CreatedByUserId=Guid.NewGuid()};
        db.Documents.Add(legacy);await db.SaveChangesAsync();await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal("Reviewed",(await db.Documents.SingleAsync(x=>x.Id==legacy.Id)).Status);
        Assert.Equal("20-12-0112/INT/HL/HSE",(await db.Documents.SingleAsync(x=>x.Id==legacy.Id)).DocumentNumber);
        Assert.Equal("27-01-0001/HV/FIN",(await new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(current.Id,V2EditingTests.Draft(),V2EditingTests.Actor(),V2EditingTests.Target())).DocumentNumber);
    }

    [CatalogSqlFact]
    public async Task Editing_the_rare_five_digit_sequence_does_not_allocate_or_reduce_it()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();
        db.DocumentNumberCounters.Add(new(){DocType="OUTGOING",Year=2027,CurrentValue=9999});await db.SaveChangesAsync();
        var first=await Register(db);
        var edited=await new V2DocumentEditor(db,V2EditingTests.Clock).UpdateAsync(first.Id,V2EditingTests.Draft(),V2EditingTests.Actor(),V2EditingTests.Target());
        Assert.Equal("27-01-10000/HV/FIN",edited.DocumentNumber);Assert.Equal(10000,edited.Registration!.SequenceNumber);
        Assert.Equal(10000,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Unicode_metadata_at_valid_limits_keeps_complete_before_and_after_audit_values()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();var first=await Register(db);
        var editor=new V2DocumentEditor(db,V2EditingTests.Clock);
        await editor.UpdateAsync(first.Id,V2EditingTests.Draft() with{Subject=new string('Đ',2000),Remark=new string('ệ',4000)},V2EditingTests.Actor(),V2EditingTests.Target());
        await editor.UpdateAsync(first.Id,V2EditingTests.Draft(2) with{Subject=new string('Ừ',2000),Remark=new string('ư',4000)},V2EditingTests.Actor());
        var audit=await db.Set<DocumentEditAudit>().SingleAsync(x=>x.Version==3);
        using var changes=System.Text.Json.JsonDocument.Parse(audit.ChangesJson);
        Assert.Equal(new string('ệ',4000),changes.RootElement.GetProperty("Remark").GetProperty("before").GetString());
        Assert.Equal(new string('ư',4000),changes.RootElement.GetProperty("Remark").GetProperty("after").GetString());
        Assert.Equal(new string('Ừ',2000),(await db.Documents.SingleAsync()).Title);
    }
    private static Task<Document> Register(DocumentDbContext db)=>new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"sql-original");
}
