using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2PersistenceSqlTests
{
    [CatalogSqlFact]
    public async Task Different_keys_across_years_do_not_deadlock_on_receipt_index_gaps()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();
        var docs=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>
        {
            await using var db=f.Db();
            return await new V2RegistrationService(db,new V2PersistenceTests.Clock(i%2==0?"2026-12-31T16:59:00Z":"2026-12-31T17:01:00Z"))
                .RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,$"years-{i}");
        }));
        Assert.Equal(20,docs.Select(x=>x.Id).Distinct().Count());await using var verify=f.Db();await VerifyCounts(verify,20);
        Assert.Equal(20,(await verify.DocumentNumberCounters.ToListAsync()).Sum(x=>x.CurrentValue));
        Assert.Equal(2,await verify.DocumentNumberCounters.CountAsync());
    }

    [CatalogSqlFact]
    public async Task Concurrent_same_registration_key_across_years_commits_one_header_receipt_event_and_number()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();
        var docs=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>
        {
            await using var db=f.Db();
            return await new V2RegistrationService(db,new V2PersistenceTests.Clock(i%2==0?"2026-12-31T16:59:00Z":"2026-12-31T17:01:00Z"))
                .RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"one-registration");
        }));
        Assert.Single(docs.Select(x=>x.Id).Distinct());Assert.Single(docs.Select(x=>x.DocumentNumber).Distinct());
        await using var verify=f.Db();await VerifyCounts(verify,1);Assert.Equal(1,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Twenty_independent_keys_share_the_counter_and_create_twenty_atomic_graphs()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();
        var docs=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>
        {
            await using var db=f.Db();var draft=V2PersistenceTests.Draft() with{CompanyCode=i%2==0?"HL":"HV"};
            return await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(draft,V2PersistenceTests.Identity,$"distinct-{i}");
        }));
        Assert.Equal(20,docs.Select(x=>x.DocumentNumber).Distinct().Count());
        await using var verify=f.Db();await VerifyCounts(verify,20);Assert.Equal(20,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Registration_keys_are_case_sensitive_despite_default_SQL_collation_and_conflicts_are_409()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();
        var service=new V2RegistrationService(db,new V2PersistenceTests.Clock());
        var a=await service.RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"Case-Key");
        var b=await service.RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"case-key");
        Assert.NotEqual(a.Id,b.Id);
        var conflict=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.RegisterAsync(V2PersistenceTests.Draft(subject:"Different"),V2PersistenceTests.Identity,"Case-Key"));
        Assert.Equal(409,conflict.Status);await VerifyCounts(db,2);
    }

    [CatalogSqlFact]
    public async Task Additive_SQL_migration_preserves_legacy_status_number_and_counter()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create("20261004060403_AddBusinessCatalogs");await using var db=f.Db();
        var old=new Document{DocType="OUTGOING",Title="Historical",Status="Reviewed",DocumentNumber="20-12-0130/HL/HSE",CreatedByUserId=Guid.NewGuid()};
        db.Documents.Add(old);db.DocumentNumberCounters.Add(new(){DocType="OUTGOING",Year=2027,CurrentValue=7});await db.SaveChangesAsync();
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var historical=await db.Documents.SingleAsync();Assert.Equal(old.DocumentNumber,historical.DocumentNumber);Assert.Equal("Reviewed",historical.Status);
        Assert.Empty(await db.DocumentRegistrations.ToListAsync());
        var registered=await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"after-upgrade");
        Assert.Equal("27-01-0008/HL/ADM",registered.DocumentNumber);Assert.Equal(8,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(old.DocumentNumber,(await db.Documents.SingleAsync(x=>x.Id==old.Id)).DocumentNumber);
    }

    [CatalogSqlFact]
    public async Task SQL_failure_rolls_back_header_receipt_event_audit_and_allocation()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();var identity=V2PersistenceTests.Identity;
        var first=await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(),identity,"first");
        var duplicate=new Document{Id=first.Id,DocType="OUTGOING",Status="InProgress",Title="Duplicate ID",CreatedByUserId=identity.InputterUserId,SenderDepartmentId=identity.OwnerDepartmentId};
        duplicate.Registration=new(){DocumentId=duplicate.Id,Kind="OUTGOING",CompanyCode="HL",CompanyNameSnapshot="Company",
            OwnerDepartmentId=identity.OwnerDepartmentId,OwnerDepartmentCodeSnapshot="ADM",OwnerDepartmentNameSnapshot="Department",
            InputterUserId=identity.InputterUserId,OriginatorUserId=identity.OriginatorUserId,LastModifierUserId=identity.InputterUserId};
        var receipt=new RegistrationRequest{ActorUserId=identity.InputterUserId,Kind="OUTGOING",KeyHash=new string('B',64),BodyHash=new string('A',64)};
        await Assert.ThrowsAsync<DbUpdateException>(()=>new DocumentRegistrationWriter(db,new V2PersistenceTests.Clock()).RegisterIdempotentV2Async(duplicate,"HL","ADM",receipt));
        Assert.False(db.ChangeTracker.HasChanges());await using var verify=f.Db();await VerifyCounts(verify,1);
        Assert.Equal(1,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    private static async Task VerifyCounts(DocumentDbContext db,int count)
    {
        Assert.Equal(count,await db.Documents.CountAsync());Assert.Equal(count,await db.DocumentRegistrations.CountAsync());
        Assert.Equal(count,await db.RegistrationRequests.CountAsync());Assert.Equal(count,await db.DocumentOutboxEvents.CountAsync());
        Assert.Equal(count,await db.DocumentStatusHistory.CountAsync());
    }
}
