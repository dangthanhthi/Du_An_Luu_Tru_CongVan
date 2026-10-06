using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace DocumentService.Tests;

public sealed class SqliteNativeVersionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Loaded_native_engine_has_the_SQLite_memory_corruption_fix()
    {
        await using var db = new SqliteConnection("Data Source=:memory:");
        await db.OpenAsync();
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        var value = (string)(await command.ExecuteScalarAsync())!;
        output.WriteLine("Loaded SQLite native version: " + value);
        Assert.True(Version.Parse(value) >= new Version(3, 50, 2), $"Loaded SQLite {value}; CVE-2025-6965 requires >=3.50.2.");
        // Exercise the actual provider after initialization, not package metadata alone.
        command.CommandText = "CREATE TABLE sample(id INTEGER PRIMARY KEY, value TEXT); INSERT INTO sample(value) VALUES ('fixture'); SELECT count(*),max(value) FROM sample";
        await using var rows = await command.ExecuteReaderAsync();
        Assert.True(await rows.ReadAsync()); Assert.Equal(1, rows.GetInt64(0)); Assert.Equal("fixture", rows.GetString(1));
    }
}
