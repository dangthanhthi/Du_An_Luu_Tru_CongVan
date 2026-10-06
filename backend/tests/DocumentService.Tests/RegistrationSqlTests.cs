using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DocumentService.Tests;

public sealed class RegistrationSqlTests
{
    [CatalogSqlFact]
    public async Task One_hundred_SQL_registrations_share_one_counter_across_company_and_department()
    {
        await using var f=await Fixture.Create();
        var docs=await Task.WhenAll(Enumerable.Range(0,100).Select(async i=>
        {
            await using var db=f.Db();
            return await new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(New("OUTGOING"),i%2==0?"HL":"HV",i%3==0?"ADM":"C&P");
        }));
        Assert.Equal(100,docs.Select(x=>x.Id).Distinct().Count());
        Assert.Equal(100,docs.Select(x=>x.DocumentNumber).Distinct().Count());
        Assert.Equal(Enumerable.Range(1,100),docs.Select(x=>int.Parse(x.DocumentNumber.Split('/')[0].Split('-')[2])).Order());
        await using var verify=f.Db();
        Assert.Equal(100,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(100,await verify.Documents.CountAsync());
        Assert.Equal(100,await verify.DocumentStatusHistory.CountAsync());
    }

    [CatalogSqlFact]
    public async Task Source_replays_racing_across_business_years_allocate_only_one_document()
    {
        await using var f=await Fixture.Create();
        var docs=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>
        {
            await using var db=f.Db();var doc=New("INCOMING");doc.SourceMessageId="one-fax";
            var clock=new Clock(i%2==0?"2026-12-31T16:59:00Z":"2026-12-31T17:01:00Z");
            return await new DocumentRegistrationWriter(db,clock).RegisterV2Async(doc,"HL",null);
        }));
        Assert.Single(docs.Select(x=>x.Id).Distinct());Assert.Single(docs.Select(x=>x.DocumentNumber).Distinct());
        await using var verify=f.Db();Assert.Equal(1,await verify.Documents.CountAsync());
        Assert.Equal(1,await verify.DocumentStatusHistory.CountAsync());
        Assert.Equal(1,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Invalid_registration_rolls_back_counter_and_document_on_SQL_Server()
    {
        await using var f=await Fixture.Create();await using var db=f.Db();
        var first=New("OUTGOING");await new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(first,"HL","ADM");
        var duplicate=New("OUTGOING");duplicate.Id=first.Id;
        duplicate.StatusHistories.Single().DocumentId=duplicate.Id;
        await Assert.ThrowsAsync<DbUpdateException>(()=>new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(duplicate,"HL","ADM"));
        Assert.False(db.ChangeTracker.HasChanges());
        await using var verify=f.Db();Assert.Equal(1,(await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(1,await verify.Documents.CountAsync());Assert.Equal(1,await verify.DocumentStatusHistory.CountAsync());
    }

    [CatalogSqlFact]
    public async Task Previously_tracked_counter_is_refreshed_before_allocating()
    {
        await using var f=await Fixture.Create();await using var a=f.Db();
        await new DocumentRegistrationWriter(a,new Clock()).RegisterV2Async(New("INTERNAL"),"HL","ADM");
        var old=await a.DocumentNumberCounters.SingleAsync();Assert.Equal(1,old.CurrentValue);
        await using(var b=f.Db()) await new DocumentRegistrationWriter(b,new Clock()).RegisterV2Async(New("INTERNAL"),"HL","ADM");
        var next=await new DocumentRegistrationWriter(a,new Clock()).RegisterV2Async(New("INTERNAL"),"HL","ADM");
        Assert.Equal("27-01-0003/INT/HL/ADM",next.DocumentNumber);
    }

    [CatalogSqlFact]
    public async Task V2_month_changes_without_reset_and_year_starts_its_own_counter()
    {
        await using var f=await Fixture.Create();await using var db=f.Db();
        var september=await new DocumentRegistrationWriter(db,new Clock("2026-09-30T16:59:00Z")).RegisterV2Async(New("INCOMING"),"HL",null);
        var october=await new DocumentRegistrationWriter(db,new Clock("2026-09-30T17:01:00Z")).RegisterV2Async(New("INCOMING"),"HV",null);
        var january=await new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(New("INCOMING"),"HL",null);
        Assert.Equal("26-09-0001/HL",september.DocumentNumber);Assert.Equal("26-10-0002/HV",october.DocumentNumber);Assert.Equal("27-01-0001/HL",january.DocumentNumber);
    }

    private static Document New(string kind)
    {
        var doc=new Document{DocType=kind,Title="SQL registration",CreatedByUserId=Guid.NewGuid(),SenderDepartmentId=kind=="INCOMING"?null:Guid.NewGuid()};
        doc.StatusHistories.Add(new(){DocumentId=doc.Id,NewStatus=doc.Status,ChangedByUserId=doc.CreatedByUserId});
        return doc;
    }
    private sealed class Clock(string value="2027-01-01T02:00:00Z"):TimeProvider{public override DateTimeOffset GetUtcNow()=>DateTimeOffset.Parse(value);}
    internal sealed class Fixture:IAsyncDisposable
    {
        private readonly string name="das_registration_qa_"+Guid.NewGuid().ToString("N");
        private readonly string master=Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        public DocumentDbContext Db()=>new(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlServer(new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString,sql=>sql.EnableRetryOnFailure(5,TimeSpan.FromMilliseconds(200),null)).Options);
        public static async Task<Fixture> Create(string? targetMigration=null)
        {
            var f=new Fixture();try{await using var c=new SqlConnection(f.master);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText="CREATE DATABASE ["+f.name+"]";await cmd.ExecuteNonQueryAsync();await using var db=f.Db();await db.GetService<IMigrator>().MigrateAsync(targetMigration);return f;}
            catch{await f.DisposeAsync();throw;}
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();await using var c=new SqlConnection(master);await c.OpenAsync();await using var cmd=c.CreateCommand();
            cmd.CommandText="IF DB_ID('"+name+"') IS NOT NULL BEGIN ALTER DATABASE ["+name+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+name+"]; END";await cmd.ExecuteNonQueryAsync();
        }
    }
}
