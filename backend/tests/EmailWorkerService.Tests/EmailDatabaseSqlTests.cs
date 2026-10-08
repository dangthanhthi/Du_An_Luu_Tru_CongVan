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

    [EmailSqlFact]
    public async Task Fixed_settings_key_can_be_inserted_and_updated_but_a_second_key_is_rejected()
    {
        await using var f = await Fixture.Create(); await using var db = f.Db(); await db.Database.MigrateAsync();
        var settings = new EmailImapSettings { EmailAddress = "singleton@example.invalid", AppPassword = "!synthetic-unusable" };
        db.EmailImapSettings.Add(settings); await db.SaveChangesAsync();
        Assert.Equal(1, settings.Id); settings.AutoScanIntervalMinutes = 43; await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); Assert.Equal(43, (await db.EmailImapSettings.SingleAsync()).AutoScanIntervalMinutes);
        db.EmailImapSettings.Add(new EmailImapSettings { Id = 2 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(1, (await db.EmailImapSettings.SingleAsync()).Id);
    }

    [EmailSqlFact]
    public async Task Identity_to_fixed_key_migration_and_rollback_preserve_existing_configuration()
    {
        await using var f = await Fixture.Create(); await using var db = f.Db();
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261006121013_EmailWorkerBaseline");
        await db.Database.ExecuteSqlRawAsync("""
            SET IDENTITY_INSERT [emailworker].[EmailImapSettings] ON;
            INSERT INTO [emailworker].[EmailImapSettings] ([Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt])
            VALUES (1,N'mail.example.invalid',993,1,N'existing@example.invalid',N'!synthetic-preserved',N'example.invalid',43,'2027-01-04T01:00:00');
            SET IDENTITY_INSERT [emailworker].[EmailImapSettings] OFF;
            """);
        var original = System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync());
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync()));
        await migrator.MigrateAsync("20261006121013_EmailWorkerBaseline"); db.ChangeTracker.Clear();
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync()));
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        var saved = await db.EmailImapSettings.SingleAsync(); saved.AutoScanIntervalMinutes = 44; await db.SaveChangesAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.Equal(44,(await db.EmailImapSettings.SingleAsync()).AutoScanIntervalMinutes);
    }

    [EmailSqlFact]
    public async Task Fixed_key_migration_rejects_unexpected_legacy_keys_without_losing_rows_or_stamping_history()
    {
        await using var f = await Fixture.Create(); await using var db = f.Db();
        await db.GetService<IMigrator>().MigrateAsync("20261006121013_EmailWorkerBaseline");
        await db.Database.ExecuteSqlRawAsync("""
            SET IDENTITY_INSERT [emailworker].[EmailImapSettings] ON;
            INSERT INTO [emailworker].[EmailImapSettings] ([Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt])
            VALUES (2,N'mail.example.invalid',993,1,N'legacy@example.invalid',N'!synthetic-preserved',N'example.invalid',43,'2027-01-04T01:00:00');
            SET IDENTITY_INSERT [emailworker].[EmailImapSettings] OFF;
            """);
        var original = System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync());
        await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync()); db.ChangeTracker.Clear();
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync()));
        Assert.Single(await db.Database.GetAppliedMigrationsAsync()); Assert.Single(await db.Database.GetPendingMigrationsAsync());
    }

    [EmailSqlFact]
    public async Task Fixed_key_migration_preserves_custom_objects_by_refusing_automatic_rebuild()
    {
        foreach (var customization in new[] {
            "CREATE INDEX [IX_SyntheticMailbox] ON [emailworker].[EmailImapSettings] ([EmailAddress]);",
            "CREATE ROLE [SyntheticMailboxReader]; GRANT SELECT ON [emailworker].[EmailImapSettings] TO [SyntheticMailboxReader];",
            "CREATE TABLE [emailworker].[SyntheticMailboxReference] ([SettingsId] int NOT NULL REFERENCES [emailworker].[EmailImapSettings]([Id]));",
            "ALTER TABLE [emailworker].[EmailImapSettings] ADD CONSTRAINT [CK_SyntheticPositivePort] CHECK ([ImapPort] > 0);",
            "ALTER TABLE [emailworker].[EmailImapSettings] ADD CONSTRAINT [DF_SyntheticPort] DEFAULT (993) FOR [ImapPort];" })
        foreach (var rollback in new[] { false, true })
        {
            await using var f = await Fixture.Create(); await using var db = f.Db();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261006121013_EmailWorkerBaseline");
            await db.Database.ExecuteSqlRawAsync("""
                SET IDENTITY_INSERT [emailworker].[EmailImapSettings] ON;
                INSERT INTO [emailworker].[EmailImapSettings] ([Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt])
                VALUES (1,N'mail.example.invalid',993,1,N'preserved@example.invalid',N'!synthetic-preserved',N'example.invalid',43,'2027-01-04T01:00:00');
                SET IDENTITY_INSERT [emailworker].[EmailImapSettings] OFF;
                """);
            if (rollback) await db.Database.MigrateAsync();
            var original = System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync());
            var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            await db.Database.ExecuteSqlRawAsync(customization);
            var error = await Assert.ThrowsAsync<SqlException>(() => rollback
                ? migrator.MigrateAsync("20261006121013_EmailWorkerBaseline") : db.Database.MigrateAsync());
            Assert.Equal(51002,error.Number);
            Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(await db.EmailImapSettings.AsNoTracking().SingleAsync()));
            // Re-run the catalog gate after rollback to prove the custom object/permission is still there.
            var count = await db.Database.SqlQueryRaw<int>("""
                SELECT (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'emailworker.EmailImapSettings') AND index_id>0 AND is_primary_key=0)
                +(SELECT COUNT(*) FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                +(SELECT COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                +(SELECT COUNT(*) FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                +(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings') AND name<>N'CK_EmailImapSettings_Singleton') AS [Value]
                """).SingleAsync();
            Assert.Equal(1,count);
        }
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
