using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;
namespace DocumentService.Tests;

public sealed class DatabaseStartupTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    [Fact]
    public void Explicit_provider_honors_nonstandard_SQL_host_and_configured_SQLite_path()
    {
        var sql = DatabaseStartupOptions.Read(Config(("Database:Provider", "SqlServer"), ("ConnectionStrings:Default", "Server=remote-db;Database=das;Integrated Security=true")), true);
        Assert.Equal("SqlServer", sql.Provider);
        Assert.StartsWith("Server=remote-db;", sql.ConnectionString);
        var local = DatabaseStartupOptions.Read(Config(("Database:Provider", "Sqlite"), ("ConnectionStrings:Default", "Data Source=custom-local.sqlite")), true);
        Assert.Equal("Data Source=custom-local.sqlite", local.ConnectionString);
    }

    [Theory]
    [InlineData(null, "Data Source=local.db", true, null)]
    [InlineData("Guess", "Data Source=local.db", true, null)]
    [InlineData("Sqlite", "Server=remote;Database=das", true, null)]
    [InlineData("SqlServer", "Data Source=local.db", true, null)]
    [InlineData("Sqlite", null, true, null)]
    [InlineData("Sqlite", "Data Source=local.db", false, null)]
    [InlineData("SqlServer", "Server=remote;Database=das;Integrated Security=true", false, "true")]
    [InlineData("SqlServer", "Server=remote;Database=das;Integrated Security=true", true, "typo")]
    public void Invalid_or_unsafe_startup_configuration_is_rejected(string? provider, string? connection, bool development, string? initialize)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Database:Provider"] = provider, ["ConnectionStrings:Default"] = connection, ["Database:Initialize"] = initialize }).Build();
        Assert.Throws<InvalidOperationException>(() => DatabaseStartupOptions.Read(config, development));
    }

    [Fact]
    public async Task Legacy_SQLite_upgrade_preserves_numbers_counters_and_repeats_without_reseeding_edits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var document = new Document { DocumentNumber = "20-12-0130/HL/C&P", DocType = "INCOMING", Title = "Historical", CreatedByUserId = Guid.NewGuid() };
        db.Documents.Add(document); db.DocumentNumberCounters.Add(new() { DocType = "INCOMING", Year = 2020, CurrentValue = 130 }); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE BusinessCatalogEntries; DROP TABLE DistributionTargets; DROP TABLE CatalogAuditEvents;");
        await SqliteG1Upgrade.ApplyAsync(db);
        Assert.Equal(21, await db.BusinessCatalogEntries.CountAsync()); Assert.Equal(14, await db.DistributionTargets.CountAsync());
        var entry = await db.BusinessCatalogEntries.FirstAsync(); entry.Name = "Local edit"; entry.IsActive = false; await db.SaveChangesAsync();
        await SqliteG1Upgrade.ApplyAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal("20-12-0130/HL/C&P", (await db.Documents.SingleAsync()).DocumentNumber);
        Assert.Equal(130, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal("Local edit", (await db.BusinessCatalogEntries.SingleAsync(x => x.Id == entry.Id)).Name);
        Assert.False((await db.BusinessCatalogEntries.SingleAsync(x => x.Id == entry.Id)).IsActive);
        Assert.All(await db.DistributionTargets.ToListAsync(), x => Assert.Equal("Pending", x.MappingState));
    }

    [Fact]
    public async Task Fresh_SQLite_is_created_transactionally_with_seed_and_can_restart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await SqliteG1Upgrade.ApplyAsync(db); await SqliteG1Upgrade.ApplyAsync(db);
        Assert.Equal(21, await db.BusinessCatalogEntries.CountAsync()); Assert.Empty(await db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Malformed_existing_G1_table_fails_before_any_missing_table_is_created()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE BusinessCatalogEntries; DROP TABLE DistributionTargets; CREATE TABLE BusinessCatalogEntries (Id TEXT PRIMARY KEY);");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'DistributionTargets'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Unknown_or_incomplete_legacy_database_is_not_silently_replaced()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE ImportantCustomerData (Id TEXT); INSERT INTO ImportantCustomerData VALUES ('keep');"; await command.ExecuteNonQueryAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
        command.CommandText = "SELECT Id FROM ImportantCustomerData"; Assert.Equal("keep", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Failure_mid_upgrade_rolls_back_every_added_table_and_seed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var fault = new UpgradeFault();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).AddInterceptors(fault).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE BusinessCatalogEntries; DROP TABLE DistributionTargets; DROP TABLE CatalogAuditEvents;");
        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => SqliteG1Upgrade.ApplyAsync(db));
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('BusinessCatalogEntries','DistributionTargets','CatalogAuditEvents')";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        fault.Enabled = false; await SqliteG1Upgrade.ApplyAsync(db);
        Assert.Equal(21, await db.BusinessCatalogEntries.CountAsync());
    }

    private sealed class UpgradeFault : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int creates;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("CREATE TABLE", StringComparison.Ordinal) && ++creates == 2)
                throw new IOException("Injected local upgrade failure");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Changed_partial_index_predicate_is_rejected_even_when_name_columns_and_uniqueness_match()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Documents_SourceMessageId; CREATE UNIQUE INDEX IX_Documents_SourceMessageId ON Documents (SourceMessageId) WHERE SourceMessageId = 'one-value';");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
    }

    [Fact]
    public async Task Missing_foreign_key_is_rejected_even_when_columns_primary_key_and_indexes_match()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DROP TABLE DocumentAttachments;
            CREATE TABLE DocumentAttachments (Id TEXT NOT NULL PRIMARY KEY, DocumentId TEXT NOT NULL, FileId TEXT NOT NULL, AttachmentType TEXT NULL, CreatedAt TEXT NOT NULL);
            CREATE INDEX IX_DocumentAttachments_DocumentId ON DocumentAttachments (DocumentId);
            """);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteG1Upgrade.ApplyAsync(db));
    }

    [Fact]
    public async Task Concurrent_local_upgrade_attempts_serialize_without_duplicate_seeds()
    {
        var path = Path.Combine(Path.GetTempPath(), "das-upgrade-" + Guid.NewGuid() + ".db");
        var options = new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite("Data Source=" + path).Options;
        try
        {
            await using (var legacy = new DocumentDbContext(options))
            {
                await legacy.Database.EnsureCreatedAsync();
                await legacy.Database.ExecuteSqlRawAsync("DROP TABLE BusinessCatalogEntries; DROP TABLE DistributionTargets; DROP TABLE CatalogAuditEvents;");
            }
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await using var db = new DocumentDbContext(options); await SqliteG1Upgrade.ApplyAsync(db);
            })));
            await using var verify = new DocumentDbContext(options);
            Assert.Equal(21, await verify.BusinessCatalogEntries.CountAsync()); Assert.Equal(14, await verify.DistributionTargets.CountAsync());
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
    }
}
