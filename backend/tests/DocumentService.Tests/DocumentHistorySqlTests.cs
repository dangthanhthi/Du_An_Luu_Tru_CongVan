using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;
public sealed class DocumentHistorySqlTests
{
    [CatalogSqlFact]
    public async Task Sql_retry_strategy_and_watermark_read_real_lifecycle_with_maximum_unicode_reasons()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create(); Guid id; var reason=new string('ế',4000); var actor=V2EditingTests.Actor();
        await using(var db=f.Db()) {var d=await V2LifecycleTests.Register(db); id=d.Id; d=await V2LifecycleTests.Change(db,d,V2StatusAction.Cancel,reason); await V2LifecycleTests.Change(db,d,V2StatusAction.Restore);}
        await using(var db=f.Db()) {
            var page=await new V2LifecycleHistory(db).ReadAsync(id,1,1,null,new(actor,new HashSet<Guid>{id}),default);
            Assert.Equal("3",page.ThroughVersion); Assert.Equal(2,page.TotalCount); Assert.Equal(reason,Assert.Single(page.Items).CancellationReason); Assert.Equal("InProgress",page.Items[0].ToStatus);
        }
        await using(var db=f.Db()) {var d=await db.Documents.Include(x=>x.Registration).SingleAsync(); await V2LifecycleTests.Change(db,d,V2StatusAction.Cancel,"Next cycle");}
        await using(var db=f.Db()) {var page=await new V2LifecycleHistory(db).ReadAsync(id,2,1,3,new(actor,new HashSet<Guid>{id}),default); Assert.Equal(2,page.TotalCount); Assert.Equal(reason,Assert.Single(page.Items).CancellationReason); Assert.Equal("Cancel",page.Items[0].Action);}
    }
}
