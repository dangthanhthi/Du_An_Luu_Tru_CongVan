using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerReadinessSqlTests
{
    [PartnerSqlFact]
    public async Task Production_SQL_starts_only_after_separate_migrations_without_seeding()
    {
        await using var fixture = await PartnerSqlTests.Fixture.Create(); await using var db = fixture.Db();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await using (var host = new Host(db.Database.GetConnectionString()!))
        {
            using var client = host.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partners")).StatusCode);
        }
        Assert.Empty(await db.Partners.IgnoreQueryFilters().ToListAsync()); Assert.Empty(await db.PartnerAudits.ToListAsync());
        Assert.Equal(applied, await db.Database.GetAppliedMigrationsAsync());
    }

    [PartnerSqlFact]
    public async Task Production_SQL_missing_schema_blocks_without_creating_tables()
    {
        await using var fixture = await PartnerSqlTests.Fixture.Create(migrate: false); await using var db = fixture.Db();
        await using var host = new Host(db.Database.GetConnectionString()!);
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("migrations are pending", error.Message);
        await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0";
        Assert.Equal(0, await command.ExecuteScalarAsync());
    }

    private sealed class Host(string connection) : WebApplicationFactory<PartnerDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); builder.UseSetting("Jwt:Secret", new string('a', 32));
            builder.UseSetting("Database:Provider", "SqlServer"); builder.UseSetting("ConnectionStrings:Default", connection);
            builder.UseSetting("Database:InitializeOnStartup", "false"); builder.UseSetting("Database:SeedExamples", "false");
        }
    }
}
