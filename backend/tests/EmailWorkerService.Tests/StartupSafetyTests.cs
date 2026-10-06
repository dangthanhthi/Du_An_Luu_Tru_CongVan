using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using EmailWorkerService;
using EmailWorkerService.Models;
using EmailWorkerService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EmailWorkerService.Tests;

public sealed class StartupSafetyTests
{
    [Fact]
    public async Task Default_startup_does_not_register_or_run_the_email_scanner()
    {
        await using var host = new Host(); using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service is EmailBackgroundWorker);
        Assert.Equal(0, host.Processor.Calls);
    }

    [Theory]
    [InlineData("/trigger")]
    [InlineData("/trigger-scan")]
    [InlineData("/settings/test-connection")]
    public async Task Manual_transport_is_disabled_without_explicit_configuration(string endpoint)
    {
        await using var host = new Host(); using var client = host.AuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/email-worker" + endpoint, new { imapHost = "example.invalid", imapPort = 993, emailAddress = "fixture@example.test", appPassword = "fixture-only" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, host.Processor.Calls);
    }

    [Fact]
    public async Task Enabling_manual_scans_does_not_enable_the_scheduled_worker()
    {
        await using var host = new Host(manual: true); using var client = host.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/email-worker/trigger", new { })).StatusCode);
        Assert.Equal(1, host.Processor.Calls);
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service is EmailBackgroundWorker);
    }

    [Fact]
    public async Task Explicit_worker_enablement_runs_only_the_controlled_fixture_processor()
    {
        await using var host = new Host(worker: true); using var client = host.CreateClient();
        await host.Processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(host.Services.GetServices<IHostedService>(), service => service is EmailBackgroundWorker);
    }

    [Fact]
    public async Task Production_rejects_sqlite_before_creating_any_database_file()
    {
        await using var host = new Host(production: true);
        Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.False(File.Exists(host.DatabasePath));
    }

    [Fact]
    public async Task Startup_without_initialization_requires_an_existing_schema()
    {
        await using var host = new Host(initialize: false);
        Directory.CreateDirectory(Path.GetDirectoryName(host.DatabasePath)!);
        await using (var connection = new SqliteConnection("Data Source=" + host.DatabasePath)) { await connection.OpenAsync(); }
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
        await using var check = new SqliteConnection("Data Source=" + host.DatabasePath); await check.OpenAsync();
        await using var command = check.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private sealed class Processor : IEmailProcessor
    {
        public int Calls;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EmailScanResult> ProcessIncomingEmailsAsync(string triggerType = "Scheduled", CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); Entered.TrySetResult(); return Task.FromResult(new EmailScanResult(EmailsScanned: 0, DocumentsCreated: 0, Success: true, ErrorMessage: null)); }
    }

    private sealed class Host(bool worker = false, bool manual = false, bool production = false, bool initialize = true) : WebApplicationFactory<EmailBackgroundWorker>
    {
        private const string Key = "Email-startup-fixture-only-key-not-a-live-secret";
        private readonly string root = Path.Combine(Path.GetTempPath(), "das-email-startup-" + Guid.NewGuid().ToString("N"));
        public string DatabasePath => Path.Combine(root, "fixture.db");
        public readonly Processor Processor = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(root);
            builder.UseEnvironment(production ? "Production" : "Development");
            builder.UseSetting("Database:Provider", "Sqlite"); builder.UseSetting("Database:Initialize", initialize.ToString());
            builder.UseSetting("ConnectionStrings:Default", "Data Source=" + DatabasePath); builder.UseSetting("Jwt:Secret", Key);
            builder.UseSetting("EmailIntake:WorkerEnabled", worker.ToString()); builder.UseSetting("EmailIntake:ManualScanEnabled", manual.ToString());
            builder.ConfigureServices(services => { services.RemoveAll<IEmailProcessor>(); services.AddSingleton<IEmailProcessor>(Processor); });
        }
        public HttpClient AuthenticatedClient()
        {
            var client = CreateClient();
            var token = new JwtSecurityToken(claims: [new("sub", Guid.NewGuid().ToString())], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return client;
        }
        public override async ValueTask DisposeAsync()
        { await base.DisposeAsync(); SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
