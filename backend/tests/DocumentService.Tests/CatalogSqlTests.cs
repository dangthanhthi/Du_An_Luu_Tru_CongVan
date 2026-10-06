using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
namespace DocumentService.Tests;

public sealed class CatalogSqlTests
{
    [CatalogSqlFact]
    public async Task Upgrade_preserves_old_document_numbers_and_adds_stable_catalogs_and_pending_targets()
    {
        await using var f = await Fixture.Create(false);
        await using var db = f.Db();
        await db.GetService<IMigrator>().MigrateAsync("20260827110241_AddIncomingSourceMessageId");
        var historical = new Document { DocumentNumber = "2020-0130/HSE", DocType = "OUTGOING", Title = "Historical record", CreatedByUserId = Guid.NewGuid() };
        db.Documents.Add(historical); await db.SaveChangesAsync();
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal("2020-0130/HSE", (await db.Documents.SingleAsync()).DocumentNumber);
        Assert.Equal(21, await db.BusinessCatalogEntries.CountAsync());
        Assert.Equal(14, await db.DistributionTargets.CountAsync());
        Assert.All(await db.DistributionTargets.ToListAsync(), x => Assert.Equal("Pending", x.MappingState));
        await db.Database.MigrateAsync();
        Assert.Equal(21, await db.BusinessCatalogEntries.CountAsync());
    }
    [CatalogSqlFact]
    public async Task Duplicate_creates_racing_in_SQL_Server_leave_one_row_and_one_audit()
    {
        await using var f = await Fixture.Create();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            await using var db = f.Db();
            try { await new CatalogService(db, TimeProvider.System).CreateAsync("categories", "RACE", "Category", Guid.NewGuid()); return 201; }
            catch (CatalogRuleException e) { return e.Status; }
        }));
        Assert.Equal(1, results.Count(x => x == 201));
        Assert.Equal(9, results.Count(x => x == 409));
        await using var verify = f.Db();
        Assert.Equal(1, await verify.BusinessCatalogEntries.CountAsync(x => x.Group == "categories" && x.Code == "RACE"));
        Assert.Equal(1, await verify.CatalogAuditEvents.CountAsync());
    }
    [CatalogSqlFact]
    public async Task Two_SQL_writers_with_same_version_allow_one_metadata_and_audit_update()
    {
        await using var f = await Fixture.Create();
        Guid id;
        await using (var db = f.Db()) id = (await db.BusinessCatalogEntries.FirstAsync(x => x.Group == "methods")).Id;
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async i =>
        {
            await using var db = f.Db();
            try { await new CatalogService(db, TimeProvider.System).UpdateAsync(id, new("Changed " + i, 1, true, 1), Guid.NewGuid()); return 200; }
            catch (CatalogRuleException e) { return e.Status; }
        }));
        Assert.Equal(new[] { 200,409 }, results.Order().ToArray());
        await using var verify = f.Db();
        Assert.Equal(2, (await verify.BusinessCatalogEntries.SingleAsync(x => x.Id == id)).Version);
        Assert.Equal(1, await verify.CatalogAuditEvents.CountAsync());
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string name = "das_cat_test_" + Guid.NewGuid().ToString("N");
        private readonly string master = Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        public DocumentDbContext Db() => new(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseSqlServer(new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString).Options);
        public static async Task<Fixture> Create(bool migrate = true)
        {
            var f = new Fixture();
            try {
                await using var c = new SqlConnection(f.master); await c.OpenAsync();
                await using var command = c.CreateCommand(); command.CommandText = "CREATE DATABASE [" + f.name + "]"; await command.ExecuteNonQueryAsync();
                if (migrate) { await using var db = f.Db(); await db.Database.MigrateAsync(); }
                return f;
            } catch { await f.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools(); await using var c = new SqlConnection(master); await c.OpenAsync();
            await using var command = c.CreateCommand(); command.CommandText = "IF DB_ID('" + name + "') IS NOT NULL BEGIN ALTER DATABASE [" + name +
                "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + name + "]; END"; await command.ExecuteNonQueryAsync();
        }
    }
}
public sealed class CatalogSqlFactAttribute : FactAttribute
{
    public CatalogSqlFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION"))) Skip = "Run scripts/qa/run-directory-sql-tests.ps1 with isolated SQL Server."; }
}

