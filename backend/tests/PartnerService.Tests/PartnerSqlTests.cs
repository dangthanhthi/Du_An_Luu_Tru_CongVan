using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerSqlTests
{
    [PartnerSqlFact]
    public async Task Migration_backfills_legacy_codes_without_renaming_or_erasing_deleted_history()
    {
        await using var f=await Fixture.Create(false);await using var db=f.Db();
        await db.GetService<IMigrator>().MigrateAsync("20260810113448_InitialCreate");
        var id=Guid.NewGuid();await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [partner].[Partners] ([Id],[FullName],[ShortName],[EntityType],[TaxCode],[IsActive],[IsDeleted],[CreatedAt],[CreatedByUserId]) VALUES ({id},N'Historical',N' ab ',N'Both',N' t-1 ',1,1,SYSUTCDATETIME(),{Guid.NewGuid()})");
        await db.Database.MigrateAsync();var p=await db.Partners.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(" ab ",p.ShortName);Assert.Equal("AB",p.NormalizedShortName);Assert.Equal("T-1",p.NormalizedTaxCode);Assert.True(p.IsDeleted);Assert.Equal(1,p.Version);
        Assert.Empty(await db.PartnerAudits.ToListAsync());
        var e=await Assert.ThrowsAsync<PartnerRuleException>(()=>new PartnerBusinessService(db).CreateAsync(new("New","AB"),Guid.NewGuid()));Assert.Equal(409,e.Status);
    }
    [PartnerSqlFact]
    public async Task Legacy_trim_collision_aborts_migration_without_destroying_history_or_indexes()
    {
        await using var f=await Fixture.Create(false);await using var db=f.Db();
        await db.GetService<IMigrator>().MigrateAsync("20260810113448_InitialCreate");
        foreach(var name in new[]{"AB"," ab "})await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [partner].[Partners] ([Id],[FullName],[ShortName],[EntityType],[IsActive],[IsDeleted],[CreatedAt],[CreatedByUserId]) VALUES ({Guid.NewGuid()},N'Keep history',{name},N'Both',1,0,SYSUTCDATETIME(),{Guid.NewGuid()})");
        await Assert.ThrowsAsync<SqlException>(()=>db.Database.MigrateAsync());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        await db.Database.OpenConnectionAsync();await using var cmd=db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM [partner].[Partners]";Assert.Equal(2,await cmd.ExecuteScalarAsync());
        cmd.CommandText="SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'partner.Partners') AND name=N'IX_Partners_ShortName'";Assert.Equal(1,await cmd.ExecuteScalarAsync());
        cmd.CommandText="SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'partner.Partners') AND name=N'Version'";Assert.Equal(0,await cmd.ExecuteScalarAsync());
    }
    [PartnerSqlFact]
    public async Task Concurrent_create_reserves_one_code_and_one_audit()
    {
        await using var f=await Fixture.Create();
        var statuses=await Task.WhenAll(Enumerable.Range(0,6).Select(async i=>{
            await using var db=f.Db();try{await new PartnerBusinessService(db).CreateAsync(new("Entity "+i,i%2==0?"race":"RACE"),Guid.NewGuid());return 201;}
            catch(PartnerRuleException e){return e.Status;}
        }));Assert.Equal(1,statuses.Count(x=>x==201));Assert.Equal(5,statuses.Count(x=>x==409));
        await using var verify=f.Db();Assert.Equal(1,await verify.Partners.CountAsync());Assert.Equal(1,await verify.PartnerAudits.CountAsync());
    }
    [PartnerSqlFact]
    public async Task Concurrent_update_has_one_winner_and_no_orphan_audit()
    {
        await using var f=await Fixture.Create();Guid id;
        await using(var db=f.Db())id=(await new PartnerBusinessService(db).CreateAsync(new("Original"),Guid.NewGuid())).Id;
        var statuses=await Task.WhenAll(Enumerable.Range(0,2).Select(async i=>{
            await using var db=f.Db();try{await new PartnerBusinessService(db).UpdateAsync(id,new("Changed "+i,1),Guid.NewGuid());return 200;}
            catch(PartnerRuleException e){return e.Status;}
        }));Assert.Equal(new[]{200,409},statuses.Order());
        await using var verify=f.Db();Assert.Equal(2,(await verify.Partners.SingleAsync()).Version);Assert.Equal(2,await verify.PartnerAudits.CountAsync());
    }
    [PartnerSqlFact]
    public async Task Null_codes_are_not_unique_and_soft_delete_restore_is_durable_in_new_context()
    {
        await using var f=await Fixture.Create();Guid id;
        await using(var db=f.Db()){
            var s=new PartnerBusinessService(db);id=(await s.CreateAsync(new("One"),Guid.NewGuid())).Id;await s.CreateAsync(new("Two"),Guid.NewGuid());
            await s.ChangeDeletionAsync(id,true,1,Guid.NewGuid());
        }
        await using(var db=f.Db()){
            var s=new PartnerBusinessService(db);Assert.False((await s.GetByIdAsync(id))!.IsActive);
            Assert.Equal(1,(await s.GetListAsync(new(null,null,true))).TotalCount);await s.ChangeDeletionAsync(id,false,2,Guid.NewGuid());
        }
        await using var verify=f.Db();Assert.Equal(2,await verify.Partners.CountAsync());Assert.Equal(4,await verify.PartnerAudits.CountAsync());
    }
    internal sealed class Fixture:IAsyncDisposable
    {
        private readonly string name="das_partner_test_"+Guid.NewGuid().ToString("N");
        private readonly string master=Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        internal PartnerDbContext Db()=>new(new DbContextOptionsBuilder<PartnerDbContext>().UseSqlServer(new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString).Options);
        internal static async Task<Fixture> Create(bool migrate=true)
        {
            var f=new Fixture();try{
                await using var c=new SqlConnection(f.master);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText="CREATE DATABASE ["+f.name+"]";await cmd.ExecuteNonQueryAsync();
                if(migrate){await using var db=f.Db();await db.Database.MigrateAsync();}return f;
            }catch{await f.DisposeAsync();throw;}
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();await using var c=new SqlConnection(master);await c.OpenAsync();await using var cmd=c.CreateCommand();
            cmd.CommandText="IF DB_ID('"+name+"') IS NOT NULL BEGIN ALTER DATABASE ["+name+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+name+"]; END";await cmd.ExecuteNonQueryAsync();
        }
    }
}
public sealed class PartnerSqlFactAttribute:FactAttribute
{public PartnerSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))Skip="Run isolated partner SQL QA.";}}
