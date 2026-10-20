using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PartnerService.Tests;
public sealed class PartnerAuditSqlTests
{
    [PartnerSqlFact]
    public async Task Sql_history_snapshot_retains_soft_delete_and_watermark_with_persisted_writer_utc()
    {
        await using var f=await PartnerSqlTests.Fixture.Create(); var actor=Guid.NewGuid(); Guid id;
        await using(var db=f.Db()) {var writer=new PartnerBusinessService(db); id=(await writer.CreateAsync(new("SQL audit"),actor)).Id; await writer.UpdateAsync(id,new("Changed",1),actor); await writer.ChangeDeletionAsync(id,true,2,actor);}
        await using(var db=f.Db()) {
            var page=await new PartnerAuditQuery(db).ReadAsync(id,1,2,null,default); Assert.Equal("3",page.ThroughVersion); Assert.Equal(new[]{"Delete","Update"},page.Items.Select(x=>x.Action));
            Assert.All(page.Items,x=>Assert.Equal(DateTimeKind.Utc,x.OccurredAt.Kind));
        }
        await using(var db=f.Db()) await new PartnerBusinessService(db).ChangeDeletionAsync(id,false,3,actor);
        await using(var db=f.Db()) {var page=await new PartnerAuditQuery(db).ReadAsync(id,2,2,3,default); Assert.Equal(3,page.TotalCount); Assert.Equal("Create",Assert.Single(page.Items).Action);}
    }
}
