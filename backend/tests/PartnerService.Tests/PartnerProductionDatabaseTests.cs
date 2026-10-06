using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerProductionDatabaseTests
{
    [Fact]
    public void Production_SQLite_cannot_start_even_with_a_valid_signing_key()
    {
        using var host = new Host();
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Database", error.Message);
    }

    private sealed class Host : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); builder.UseSetting("Jwt:Secret", new string('a', 32));
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:Default", "Data Source=synthetic-production-invalid.db;Mode=Memory;Cache=Shared");
            builder.UseSetting("Database:InitializeOnStartup", "false"); builder.UseSetting("Database:SeedExamples", "false");
        }
    }
}
