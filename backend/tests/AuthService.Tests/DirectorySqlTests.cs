using AuthService.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
namespace AuthService.Tests;

public sealed class DirectorySqlTests
{
    [SqlFact]
    public async Task Migrations_add_only_owned_tables_and_preserve_legacy_departments_on_upgrade()
    {
        await using var f = await SqlFixture.Create(migrate: false);
        await using var db = f.Db();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260810130346_ImproveAuthModel");
        var department = new Department { Code = "HSE", Name = "Historical HSE", IsActive = true };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal("HSE", (await db.Departments.SingleAsync()).Code);
        Assert.Contains((await db.Database.GetAppliedMigrationsAsync()), x => x.EndsWith("_AddDirectoryProjection"));
        await new OrganizationDirectoryStore(db, f.Clock).ApplyAsync("eap", "one", DirectoryPersistenceTests.Snapshot());
        Assert.Equal(1, await db.DirectoryProjections.CountAsync());
        Assert.Equal("HSE", (await db.Departments.SingleAsync()).Code);
    }

    [SqlFact]
    public async Task Twenty_concurrent_deliveries_of_one_message_commit_one_revision_receipt_and_event()
    {
        await using var f = await SqlFixture.Create();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = f.Db();
            return await new OrganizationDirectoryStore(db, f.Clock).ApplyAsync("eap", "same", DirectoryPersistenceTests.Snapshot());
        }));
        Assert.All(results, x => Assert.Equal(DirectorySyncDisposition.Applied, x.Disposition));
        Assert.Equal(19, results.Count(x => x.IsDuplicate));
        await using var verify = f.Db();
        Assert.Equal(1, await verify.DirectoryProjections.CountAsync());
        Assert.Equal(1, await verify.DirectoryInbox.CountAsync());
        Assert.Equal(1, await verify.DirectoryOutbox.CountAsync());
    }

    [SqlFact]
    public async Task Competing_out_of_order_writers_finish_at_highest_sequence_without_reactivating_revoked_member()
    {
        await using var f = await SqlFixture.Create();
        await Task.WhenAll(Enumerable.Range(1, 20).Reverse().Select(async i =>
        {
            await using var db = f.Db();
            var snapshot = DirectoryPersistenceTests.Snapshot(i);
            if (i == 20) snapshot = snapshot with { Memberships = [] };
            await new OrganizationDirectoryStore(db, f.Clock).ApplyAsync("eap", "message-" + i, snapshot);
        }));
        await using var verify = f.Db();
        var head = await verify.DirectoryProjections.SingleAsync();
        Assert.Equal(20, head.Sequence);
        Assert.Equal(head.AuthorizationRevision, await verify.DirectoryOutbox.CountAsync());
        var current = await new OrganizationDirectoryStore(verify, f.Clock).ReadFreshAsync("eap", TimeSpan.FromSeconds(60));
        Assert.Empty(current.Projection.GetActiveDepartmentIds(DirectoryPersistenceTests.Staff));
        Assert.Equal(20, await verify.DirectoryInbox.CountAsync());
    }

    [SqlFact]
    public async Task Exception_after_SQL_save_rolls_back_all_three_tables()
    {
        await using var f = await SqlFixture.Create();
        await using (var db = f.Db(new CrashAfterSave()))
            await Assert.ThrowsAsync<CrashException>(() => new OrganizationDirectoryStore(db, f.Clock)
                .ApplyAsync("eap", "crash", DirectoryPersistenceTests.Snapshot()));
        await using var verify = f.Db();
        Assert.Equal(0, await verify.DirectoryProjections.CountAsync());
        Assert.Equal(0, await verify.DirectoryInbox.CountAsync());
        Assert.Equal(0, await verify.DirectoryOutbox.CountAsync());
    }

    [SqlFact]
    public async Task Independent_sources_and_conflicting_envelopes_cannot_overwrite_each_other()
    {
        await using var f = await SqlFixture.Create();
        await using var db = f.Db();
        var store = new OrganizationDirectoryStore(db, f.Clock);
        await store.ApplyAsync("one", "same", DirectoryPersistenceTests.Snapshot());
        await store.ApplyAsync("two", "same", DirectoryPersistenceTests.Snapshot(7));
        Assert.Equal(DirectorySyncDisposition.MessageConflict,
            (await store.ApplyAsync("one", "same", DirectoryPersistenceTests.Snapshot(8))).Disposition);
        Assert.Equal(2, await db.DirectoryProjections.CountAsync());
        Assert.Equal(1, (await store.ReadFreshAsync("one", TimeSpan.FromSeconds(60))).Projection.Sequence);
        Assert.Equal(7, (await store.ReadFreshAsync("two", TimeSpan.FromSeconds(60))).Projection.Sequence);
    }

    [SqlFact]
    public async Task Stored_receipt_replay_after_restart_keeps_verification_time_and_permissions_revoked()
    {
        await using var f = await SqlFixture.Create();
        await using (var db = f.Db())
        {
            await new OrganizationDirectoryStore(db, f.Clock).ApplyAsync("eap", "revoke",
                DirectoryPersistenceTests.Snapshot(2) with { Memberships = [] });
        }
        f.Clock.Now += TimeSpan.FromSeconds(61);
        await using var restarted = f.Db();
        var store = new OrganizationDirectoryStore(restarted, f.Clock);
        var replay = await store.ApplyAsync("eap", "revoke", DirectoryPersistenceTests.Snapshot(2) with { Memberships = [] });
        Assert.True(replay.IsDuplicate);
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60)));
        Assert.Equal(1, await restarted.DirectoryOutbox.CountAsync());
    }

    private sealed class CrashException : Exception;
    private sealed class CrashAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
            => throw new CrashException();
    }
    internal sealed class SqlFixture : IAsyncDisposable
    {
        private readonly string databaseName = "das_dir_test_" + Guid.NewGuid().ToString("N");
        private readonly string master = Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        private string Connection => new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        public DirectoryPersistenceTests.ManualClock Clock { get; } = new();
        public AuthDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(Connection, options => options.EnableRetryOnFailure(3))
            .AddInterceptors(interceptors).Options);
        public static async Task<SqlFixture> Create(bool migrate = true)
        {
            var f = new SqlFixture();
            try
            {
                await using var conn = new SqlConnection(f.master);
                await conn.OpenAsync();
                await using var command = conn.CreateCommand();
                command.CommandText = "CREATE DATABASE [" + f.databaseName + "]";
                await command.ExecuteNonQueryAsync();
                if (migrate) { await using var db = f.Db(); await db.Database.MigrateAsync(); }
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var conn = new SqlConnection(master);
            await conn.OpenAsync();
            await using var command = conn.CreateCommand();
            command.CommandText = "IF DB_ID('" + databaseName + "') IS NOT NULL BEGIN ALTER DATABASE [" +
                databaseName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + databaseName + "]; END";
            await command.ExecuteNonQueryAsync();
        }
    }
}
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))
            Skip = "Requires SQL Server: run scripts/qa/run-directory-sql-tests.ps1. Not a SQLite substitute.";
    }
}

