using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class SubsequentSqlTests
{
    [CatalogSqlFact] public async Task Parallel_weekly_planners_and_dispatchers_have_one_durable_winner()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();Document doc;
        await using(var db=f.Db())doc=await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft("INTERNAL") with{Sensitivity="Normal",IssuedDate=null,Details=new()},V2PersistenceTests.Identity,"sql-reminder");
        var clock=new V2PersistenceTests.Clock("2027-01-18T01:00:00Z");var directory=new Directory(doc.Registration!.OwnerDepartmentId,doc.Registration.InputterUserId);var transport=new Transport();
        WeeklyReminders Service(DocumentDbContext db)=>new(db,new(db,new(db,new CurrentPdfTests.Files()),clock),directory,transport,clock);
        var plans=await Task.WhenAll(Enumerable.Range(0,12).Select(async _=>{await using var db=f.Db();return await Service(db).PlanAsync(directory.Department);}));Assert.Single(plans.Select(x=>x.Id).Distinct());Assert.Equal("Planned",plans[0].State);
        var sent=await Task.WhenAll(Enumerable.Range(0,12).Select(async _=>{await using var db=f.Db();return await Service(db).DispatchAsync(plans[0].Id);}));Assert.True(sent.Count(x=>x=="Queued")==1,"Dispatch states: "+string.Join(",",sent));Assert.Equal(1,transport.Calls);
        await using var check=f.Db();Assert.Single(await check.Set<ReminderBatch>().ToListAsync());Assert.Equal("Queued",(await check.Set<ReminderBatch>().SingleAsync()).State);
    }
    [CatalogSqlFact] public async Task Additive_migration_preserves_headers_numbers_and_pdf_projection()
    {await using var f=await RegistrationSqlTests.Fixture.Create("20261005024036_AddCurrentPdfReplacement");await using var db=f.Db();var doc=await V2LifecycleTests.Register(db);var before=doc.DocumentNumber;await db.Database.MigrateAsync();Assert.Equal(before,(await db.Documents.SingleAsync()).DocumentNumber);Assert.Empty(await db.Set<ReminderBatch>().ToListAsync());Assert.Empty(await db.Set<DocumentTaskIntent>().ToListAsync());Assert.Equal(1,(await db.DocumentRegistrations.SingleAsync()).Version);}
    private sealed class Directory(Guid dep,Guid user):IReminderDirectory
    {public Guid Department=dep;public Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([Department]);public Task<ReminderDirectory> ResolveAsync(Guid d,CancellationToken ct)=>Task.FromResult(new ReminderDirectory(d,new(user,true,true,true,false,new HashSet<Guid>{Department},new HashSet<Guid>(),new HashSet<Guid>()),[new(user,true,"inputter@example.test")],[new(Guid.NewGuid(),true,"leader@example.test")]));}
    private sealed class Transport:IReminderTransport{public int Calls;public async Task<string> EnqueueAsync(Guid id,ReminderPlan plan,CancellationToken ct){Interlocked.Increment(ref Calls);await Task.Delay(50,ct);return "Queued";}}
}
