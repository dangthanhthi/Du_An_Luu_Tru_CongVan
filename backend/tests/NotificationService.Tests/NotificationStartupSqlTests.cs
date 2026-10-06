using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using Xunit;

namespace NotificationService.Tests;

public sealed class NotificationStartupSqlTests
{
    [NotificationSqlFact]
    public async Task Development_sql_initialization_records_migrations_and_allows_readonly_restart()
    {
        await using var fixture = await NotificationSqlTests.Fixture.Create(migrate: false);
        await using var db = fixture.Db();
        var connection = db.Database.GetConnectionString()!;
        await using (var host = new Host(connection, initialize: true))
        {
            using var client = host.CreateClient();
            Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);
        }
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await using var production = new Host(connection, initialize: false);
        using var restarted = production.CreateClient();
        Assert.True((await restarted.GetAsync("/health")).IsSuccessStatusCode);
    }

    private sealed class Host(string connection, bool initialize) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(initialize ? "Development" : "Production");
            builder.UseSetting("Jwt:Secret", "Notification-SQL-fixture-only-signing-key");
            builder.UseSetting("Database:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:Default", connection);
            builder.UseSetting("Database:Initialize", initialize.ToString());
            builder.UseSetting("Delivery:WorkerEnabled", "false"); builder.UseSetting("Smtp:DeliveryEnabled", "false");
        }
    }
}
