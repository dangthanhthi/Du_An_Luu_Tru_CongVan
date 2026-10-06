using EmailWorkerService.Data;
using EmailWorkerService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;
using Xunit;

namespace EmailWorkerService.Tests;

public sealed class EmailDatabaseSqlTests
{
    [EmailSqlFact]
    public async Task Production_requires_reviewed_migrations_without_creating_tables()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Options(false).VerifyAsync(db, development: false));
        Assert.Contains("Apply reviewed email SQL migrations", error.Message);
        Assert.Equal(0, await fixture.TableCount());
    }

    [EmailSqlFact]
    public async Task EnsureCreated_sql_is_rejected_without_adopting_history_or_changing_rows()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        await db.Database.EnsureCreatedAsync();
        db.EmailScanLogs.Add(new EmailScanLog { TriggerType = "Manual", Success = true });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Options(false).VerifyAsync(db, development: false));
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Single(await db.EmailScanLogs.ToListAsync());
        Assert.Equal(3, await fixture.TableCount());
    }

    [EmailSqlFact]
    public async Task Development_sql_initialization_uses_migrations_and_production_can_verify_it()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        await fixture.Options(true).VerifyAsync(db, development: true);
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await fixture.Options(false).VerifyAsync(db, development: false);
        Assert.Empty(await db.EmailImapSettings.ToListAsync());
        Assert.Empty(await db.EmailScanLogs.ToListAsync());
        Assert.Empty(await db.EmailScanItemLogs.ToListAsync());
    }

    [EmailSqlFact]
    public async Task Idempotent_sql_replay_preserves_data_and_enforces_relationships()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await fixture.ApplyScript(script);
        var scan = new EmailScanLog { TriggerType = "Manual", Success = true };
        var item = new EmailScanItemLog { ScanLogId = scan.Id, SenderEmail = "fixture@example.test", Status = "Ready" };
        db.EmailScanLogs.Add(scan); db.EmailScanItemLogs.Add(item); await db.SaveChangesAsync();
        await fixture.ApplyScript(script);
        await fixture.Options(false).VerifyAsync(db, development: false);
        Assert.Single(await db.EmailScanLogs.ToListAsync());
        Assert.Single(await db.EmailScanItemLogs.ToListAsync());
        db.EmailScanItemLogs.Add(new EmailScanItemLog { ScanLogId = Guid.NewGuid(), SenderEmail = "invalid@example.test" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.EmailScanLogs.Remove(await db.EmailScanLogs.SingleAsync()); await db.SaveChangesAsync();
        Assert.Empty(await db.EmailScanItemLogs.ToListAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string master = Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        private readonly string name = "DAS_Email_QA_" + Guid.NewGuid().ToString("N");
        private string Connection => new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
        public EmailWorkerDbContext Db() => new(new DbContextOptionsBuilder<EmailWorkerDbContext>().UseSqlServer(Connection).Options);
        public EmailWorkerDatabaseOptions Options(bool initialize) => new("SqlServer", Connection, initialize);
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            try
            {
                await using var connection = new SqlConnection(fixture.master); await connection.OpenAsync();
                await using var command = connection.CreateCommand(); command.CommandText = "CREATE DATABASE [" + fixture.name + "]";
                await command.ExecuteNonQueryAsync(); return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async Task<int> TableCount()
        {
            await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sys.tables";
            return (int)(await command.ExecuteScalarAsync())!;
        }
        public async Task ApplyScript(string script)
        {
            await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var command = connection.CreateCommand(); command.CommandText = batch;
                await command.ExecuteNonQueryAsync();
            }
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master); await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "IF DB_ID('" + name + "') IS NOT NULL BEGIN ALTER DATABASE [" + name + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + name + "]; END";
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class EmailSqlFactAttribute : FactAttribute
{
    public EmailSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))
            Skip = "Requires the owned isolated SQL QA runner.";
    }
}
