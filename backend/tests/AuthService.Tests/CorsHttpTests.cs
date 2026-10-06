using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AuthService.Tests;

public sealed class CorsHttpTests
{
    private const string Allowed = "https://das.example.test";
    private const string Route = "/api/v2/directory/departments";

    [Theory]
    [InlineData("GET")][InlineData("OPTIONS")]
    public async Task Explicit_allowed_origin_gets_credentials_but_never_bypasses_authentication(string method)
    {
        await using var fixture = new Host();
        await using var host = fixture.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", Allowed));
        using var client = host.CreateClient(); using var request = Request(method, Allowed);
        using var response = await client.SendAsync(request);
        Assert.Equal(method == "OPTIONS" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(Allowed, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Theory]
    [InlineData("https://untrusted.example.test")]
    [InlineData("http://das.example.test")]
    [InlineData("https://das.example.test:8443")]
    [InlineData("https://sub.das.example.test")]
    [InlineData("https://das.example.test.evil.test")]
    [InlineData("null")]
    public async Task Other_origins_are_not_reflected_for_GET_or_preflight(string origin)
    {
        await using var fixture = new Host();
        await using var host = fixture.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", Allowed));
        using var client = host.CreateClient();
        foreach (var method in new[] { "GET", "OPTIONS" })
        {
            using var request = Request(method, origin); using var response = await client.SendAsync(request);
            Assert.Equal(method == "OPTIONS" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }

    [Fact]
    public async Task Request_without_browser_origin_still_requires_authentication()
    {
        await using var fixture = new Host();
        await using var host = fixture.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", Allowed));
        using var client = host.CreateClient(); using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Request(string method, string origin)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), Route);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        if (method == "OPTIONS") { request.Headers.Add("Access-Control-Request-Method", "GET"); request.Headers.Add("Access-Control-Request-Headers", "authorization"); }
        return request;
    }
    private sealed class Host : WebApplicationFactory<AuthService.AuthDbContext>
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "das-auth-cors-" + Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory); builder.UseEnvironment("Development");
            builder.UseSetting("Database:Provider", "Sqlite"); builder.UseSetting("Database:Initialize", "true");
            builder.UseSetting("Database:SeedDemoUsers", "false");
            builder.UseSetting("ConnectionStrings:Default", "Data Source=" + Path.Combine(directory, "test.db"));
            builder.UseSetting("Jwt:Secret", new string('a', 32));
        }
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var prefix = Path.GetFullPath(Path.GetTempPath()); var absolute = Path.GetFullPath(directory);
            if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("das-auth-cors-")) throw new InvalidOperationException("Owned fixture path required.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }

}
