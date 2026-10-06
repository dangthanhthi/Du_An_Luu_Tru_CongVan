using AuthService.Organization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace AuthService.Tests;

public sealed class DatabaseStartupTests
{
    [Fact]
    public void Demo_users_require_explicit_development_opt_in()
    {
        IConfiguration Config(string? seed) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Database:Provider"] = "SqlServer", ["ConnectionStrings:Default"] = "Server=test;Database=das;Integrated Security=true",
            ["Database:SeedDemoUsers"] = seed }).Build();
        Assert.False(DatabaseStartupOptions.Read(Config(null), true).SeedDemoUsers);
        Assert.True(DatabaseStartupOptions.Read(Config("true"), true).SeedDemoUsers);
        Assert.Throws<InvalidOperationException>(() => DatabaseStartupOptions.Read(Config("true"), false));
        Assert.Throws<InvalidOperationException>(() => DatabaseStartupOptions.Read(Config("typo"), true));
        Assert.False(DatabaseStartupOptions.Read(Config(null), false).Initialize);
    }

    [Fact]
    public async Task Legacy_auth_upgrade_preserves_identity_and_refresh_rows_and_survives_restart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connection).Options;
        var user = new User { Username = "old-user", FullName = "Historical user", Email = "old@example.test", PasswordHash = "retained-hash", IsActive = true };
        await using (var db = new AuthDbContext(options))
        {
            await db.Database.EnsureCreatedAsync(); db.Users.Add(user); await db.SaveChangesAsync();
            db.RefreshTokens.Add(new() { UserId = user.Id, Token = "test-refresh", ExpiresAt = DateTime.UtcNow.AddDays(1) }); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE DirectoryInbox; DROP TABLE DirectoryOutbox; DROP TABLE DirectoryProjections;");
            await SqliteG1Upgrade.ApplyAsync(db);
            var result = await new OrganizationDirectoryStore(db, TimeProvider.System).ApplyAsync("eap", "upgrade-test", DirectoryPersistenceTests.Snapshot());
            Assert.Equal(DirectorySyncDisposition.Applied, result.Disposition);
        }
        await using var restarted = new AuthDbContext(options); await SqliteG1Upgrade.ApplyAsync(restarted);
        Assert.Equal(user.Id, (await restarted.Users.SingleAsync()).Id);
        Assert.Equal("retained-hash", (await restarted.Users.SingleAsync()).PasswordHash);
        Assert.Equal("test-refresh", (await restarted.RefreshTokens.SingleAsync()).Token);
        Assert.Single(await restarted.DirectoryInbox.ToListAsync()); Assert.Single(await restarted.DirectoryOutbox.ToListAsync());
    }

    [Fact]
    public async Task Fresh_auth_database_contains_no_demo_identities_and_initialization_can_be_disabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connection).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db, false));
        await SqliteG1Upgrade.ApplyAsync(db);
        Assert.Empty(await db.Users.ToListAsync()); Assert.Empty(await db.Roles.ToListAsync());
        await SqliteG1Upgrade.ApplyAsync(db, false);
    }

    [Fact]
    public async Task Missing_unique_outbox_index_fails_closed_without_repairing_schema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_DirectoryOutbox_SourceId_AuthorizationRevision; DROP TABLE DirectoryInbox;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'DirectoryInbox'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Existing_orphan_data_is_rejected_without_silent_repair()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF; INSERT INTO UserRoles (UserId,RoleId) VALUES ('11111111-1111-4111-8111-111111111111','22222222-2222-4222-8222-222222222222'); PRAGMA foreign_keys=ON;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
        Assert.Single(await db.UserRoles.ToListAsync());
    }
}
