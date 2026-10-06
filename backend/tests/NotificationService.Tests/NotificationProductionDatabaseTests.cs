using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using Xunit;

namespace NotificationService.Tests;

public sealed class NotificationProductionDatabaseTests
{
    [Fact]
    public async Task Production_SQLite_cannot_start_even_when_the_inbox_schema_exists()
    {
        var connectionString = "Data Source=synthetic-readiness-" + Guid.NewGuid().ToString("N") + ".db;Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString); await connection.OpenAsync();
        await using (var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(connectionString).Options))
            await db.Database.EnsureCreatedAsync();
        await using var host = new Host(connectionString);
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Database", error.Message);
    }

    private sealed class Host(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); builder.UseSetting("Jwt:Secret", new string('a', 32));
            builder.UseSetting("Database:Provider", "Sqlite"); builder.UseSetting("ConnectionStrings:Default", connection);
            builder.UseSetting("Database:Initialize", "false"); builder.UseSetting("Delivery:WorkerEnabled", "false");
            builder.UseSetting("Smtp:DeliveryEnabled", "false");
        }
    }
}
