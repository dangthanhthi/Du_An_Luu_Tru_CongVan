using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthService.Tests;

public sealed class CorsSqlTests
{
    [CorsSqlTheory]
    [InlineData(false, "https://das.example.test", false)]
    [InlineData(true, "https://das.example.test", true)]
    [InlineData(true, "https://untrusted.example.test", false)]
    public async Task Production_only_grants_configured_browser_origins_without_bypassing_auth(bool configured, string origin, bool allowed)
    {
        await using var fixture = await DirectorySqlTests.SqlFixture.Create(); await using var db = fixture.Db();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await using (var host = new Host(db.Database.GetConnectionString()!, configured))
        {
            using var client = host.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            foreach (var method in new[] { "GET", "OPTIONS" })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v2/directory/departments");
                request.Headers.Add("Origin", origin);
                if (method == "OPTIONS") { request.Headers.Add("Access-Control-Request-Method", "GET"); request.Headers.Add("Access-Control-Request-Headers", "authorization"); }
                using var response = await client.SendAsync(request);
                Assert.Equal(method == "OPTIONS" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
                Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Credentials"));
                if (allowed) { Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"))); Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials"))); }
            }
        }
        Assert.Equal(applied, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await db.Users.CountAsync());
    }

    private sealed class Host(string connection, bool configured) : WebApplicationFactory<AuthService.AuthDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); builder.UseSetting("Jwt:Secret", new string('a',32));
            builder.UseSetting("Database:Provider", "SqlServer"); builder.UseSetting("ConnectionStrings:Default", connection);
            builder.UseSetting("Database:Initialize", "false"); builder.UseSetting("Database:InitializeOnStartup", "false");
            builder.UseSetting("Database:SeedExamples", "false"); builder.UseSetting("Database:SeedDemoUsers", "false");
            builder.UseSetting("Notifications:WorkerEnabled", "false"); builder.UseSetting("Reminders:Enabled", "false");
            builder.UseSetting("Delivery:WorkerEnabled", "false"); builder.UseSetting("Smtp:DeliveryEnabled", "false");
            if (configured) builder.UseSetting("Cors:AllowedOrigins:0", "https://das.example.test");
        }
    }
}

public sealed class CorsSqlTheoryAttribute : TheoryAttribute
{
    public CorsSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION"))) Skip = "Requires owned SQL Server fixture; run scripts/qa/run-cors-sql.ps1.";
    }
}
