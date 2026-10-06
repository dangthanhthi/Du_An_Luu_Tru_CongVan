using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using Xunit;

namespace NotificationService.Tests;

public sealed class NotificationReadinessSqlTests
{
    [NotificationSqlFact]
    public async Task Production_SQL_startup_keeps_existing_inbox_and_delivery_disabled()
    {
        await using var fixture = await NotificationSqlTests.Fixture.Create(); await using var db = fixture.Db();
        var receipt = await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(), "startup-fixture", new(Guid.NewGuid(), "fixture@example.test", "Synthetic", "Body", null, "Info", null), default);
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await using (var host = new Host(db.Database.GetConnectionString()!))
        {
            using var client = host.CreateClient(); var response = await client.GetAsync("/health"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var status = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.False(status.GetProperty("workerEnabled").GetBoolean()); Assert.False(status.GetProperty("smtpEnabled").GetBoolean());
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications/my")).StatusCode);
        }
        Assert.Equal(receipt.Id, (await db.Set<DeliveryInbox>().SingleAsync()).Id); Assert.Equal("Queued", (await db.Set<DeliveryInbox>().SingleAsync()).State);
        Assert.Empty(await db.NotificationLogs.ToListAsync()); Assert.Equal(applied, await db.Database.GetAppliedMigrationsAsync());
    }

    [NotificationSqlFact]
    public async Task Production_SQL_missing_schema_blocks_without_creating_tables()
    {
        await using var fixture = await NotificationSqlTests.Fixture.Create(migrate: false); await using var db = fixture.Db();
        await using var host = new Host(db.Database.GetConnectionString()!);
        var error = Assert.Throws<SqlException>(() => host.CreateClient());
        Assert.Equal(208, error.Number); // Missing inbox table, not an unrelated startup/config failure.
        await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0";
        Assert.Equal(0, await command.ExecuteScalarAsync());
    }

    private sealed class Host(string connection) : WebApplicationFactory<NotificationDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); builder.UseSetting("Jwt:Secret", new string('a', 32));
            builder.UseSetting("Database:Provider", "SqlServer"); builder.UseSetting("ConnectionStrings:Default", connection);
            builder.UseSetting("Database:Initialize", "false"); builder.UseSetting("Delivery:WorkerEnabled", "false"); builder.UseSetting("Smtp:DeliveryEnabled", "false");
        }
    }
}
