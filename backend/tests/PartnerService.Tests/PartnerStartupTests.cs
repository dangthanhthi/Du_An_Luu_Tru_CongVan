using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerStartupTests
{
    public static IEnumerable<object?[]> InvalidKeys => new[] { new object?[] { null }, [""], [" "], ["short"], [new string('a', 31)], ["REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG"] };
    public static IEnumerable<object[]> ValidKeys => new[] { new object[] { new string('a', 32) }, [new string('é', 16)] };

    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public void Invalid_signing_key_prevents_host_startup(string? key)
    {
        using var host = new Host(key);
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Jwt:Secret", error.Message);
    }

    [Theory]
    [MemberData(nameof(ValidKeys))]
    public async Task Provisioned_key_at_least_32_UTF8_bytes_keeps_health_reachable(string key)
    {
        await using var host = new Host(key, development: true); using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    private sealed class Host(string? key, bool development = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(development ? "Development" : "Production");
            builder.UseSetting("Jwt:Secret", key ?? "");
            builder.UseSetting("Database:InitializeOnStartup", "false");
            builder.UseSetting("Database:SeedExamples", "false");
            builder.UseSetting("ConnectionStrings:Default", "Data Source=synthetic-startup-unused.db;Mode=Memory;Cache=Shared");
        }
    }
}
