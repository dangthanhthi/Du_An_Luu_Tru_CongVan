using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthService;

public sealed record DatabaseStartupOptions(string Provider, string ConnectionString, bool Initialize, bool SeedDemoUsers)
{
    public static DatabaseStartupOptions Read(IConfiguration config, bool development)
    {
        var provider = config["Database:Provider"];
        var connection = config.GetConnectionString("Default");
        if (provider is not ("SqlServer" or "Sqlite") || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure Database:Provider (SqlServer or Sqlite) and ConnectionStrings:Default explicitly.");
        var initialize = Flag("Database:Initialize", development);
        var seed = Flag("Database:SeedDemoUsers", false);
        if (!development && (provider == "Sqlite" || initialize || seed))
            throw new InvalidOperationException("SQLite, startup schema changes and demo seeding are limited to Development. Apply SQL migrations separately before production startup.");
        try
        {
            if (provider == "Sqlite")
            {
                var parsed = new SqliteConnectionStringBuilder(connection);
                if (string.IsNullOrWhiteSpace(parsed.DataSource)) throw new ArgumentException();
            }
            else
            {
                var parsed = new SqlConnectionStringBuilder(connection);
                if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog)) throw new ArgumentException();
            }
        }
        catch (ArgumentException) { throw new InvalidOperationException("The configured connection string does not match the selected database provider."); }
        return new(provider, connection, initialize, seed);

        bool Flag(string name, bool fallback)
        {
            var value = config[name];
            if (value is null) return fallback;
            if (bool.TryParse(value, out var enabled)) return enabled;
            throw new InvalidOperationException($"{name} must be true or false.");
        }
    }
}

// Local Development only. SQL Server continues to use the owned-schema EF
// migrations. This adds only the three G1 directory tables to a recognized legacy SQLite
// database; it never rebuilds legacy tables or changes existing identities or projection rows.
public static class SqliteG1Upgrade
{
    private static readonly HashSet<string> AddedTables = ["DirectoryProjections", "DirectoryInbox", "DirectoryOutbox"];

    public static async Task ApplyAsync(DbContext db, bool initialize = true, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlite()) throw new InvalidOperationException("The local G1 upgrade requires SQLite.");
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var tables = operations.OfType<CreateTableOperation>().ToArray();
        var indexes = operations.OfType<CreateIndexOperation>().ToArray();
        // SQLite's SQL generator moves a single-column key inline and mutates
        // CreateTableOperation.PrimaryKey. Retain the expected key before it runs.
        var primaryKeys = tables.ToDictionary(x => x.Name, x => x.PrimaryKey?.Columns.ToArray() ?? []);
        await db.Database.OpenConnectionAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var connection = db.Database.GetDbConnection();

        async Task<HashSet<string>> ExistingTables()
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";
            await using var reader = await command.ExecuteReaderAsync(ct);
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(ct)) names.Add(reader.GetString(0));
            return names;
        }
        async Task Validate(CreateTableOperation table)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction.GetDbTransaction();
            command.CommandText = $"PRAGMA table_info(\"{table.Name}\")";
            var columns = new Dictionary<string, (string Type, bool Nullable, int Key)>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1), (reader.GetString(2), reader.GetInt64(3) == 0, reader.GetInt32(5)));
            if (columns.Count != table.Columns.Count || table.Columns.Any(x => !columns.TryGetValue(x.Name, out var column) ||
                !string.Equals(column.Type, x.ColumnType, StringComparison.OrdinalIgnoreCase) || column.Nullable != x.IsNullable) ||
                !columns.Where(x => x.Value.Key > 0).OrderBy(x => x.Value.Key).Select(x => x.Key).SequenceEqual(primaryKeys[table.Name]))
                throw new InvalidOperationException($"SQLite table {table.Name} is not a supported baseline. Back up the file and apply a reviewed migration.");
            var actualIndexes = new Dictionary<string, (bool Unique, bool Partial)>(StringComparer.Ordinal);
            command.CommandText = $"PRAGMA index_list(\"{table.Name}\")";
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) actualIndexes.Add(reader.GetString(1), (reader.GetInt64(2) != 0, reader.GetInt64(4) != 0));
            foreach (var index in indexes.Where(x => x.Table == table.Name))
            {
                if (!actualIndexes.TryGetValue(index.Name, out var actual) || actual.Unique != index.IsUnique || actual.Partial != (index.Filter is not null))
                    throw new InvalidOperationException($"SQLite index {index.Name} is missing or incompatible. Apply a reviewed migration.");
                command.CommandText = $"PRAGMA index_info(\"{index.Name}\")";
                var names = new List<string>();
                await using (var reader = await command.ExecuteReaderAsync(ct))
                    while (await reader.ReadAsync(ct)) names.Add(reader.GetString(2));
                if (!names.SequenceEqual(index.Columns)) throw new InvalidOperationException($"SQLite index {index.Name} has incompatible columns.");
                if (index.Filter is not null)
                {
                    command.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name=$name";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "$name"; parameter.Value = index.Name;
                    command.Parameters.Add(parameter);
                    var definition = await command.ExecuteScalarAsync(ct) as string ?? "";
                    command.Parameters.Clear();
                    var match = System.Text.RegularExpressions.Regex.Match(definition, @"\bWHERE\s+(.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);
                    // Exact baseline predicate comparison intentionally rejects
                    // unfamiliar equivalent forms; no semantic SQL inference.
                    if (!match.Success || match.Groups[1].Value.Trim().TrimEnd(';') != index.Filter.Trim().TrimEnd(';'))
                        throw new InvalidOperationException($"SQLite index {index.Name} has an incompatible predicate.");
                }
            }
            command.CommandText = $"PRAGMA foreign_key_list(\"{table.Name}\")";
            var actualForeignKeys = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) actualForeignKeys.Add(string.Join('|', reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
            var expectedForeignKeys = table.ForeignKeys.SelectMany(fk => fk.Columns.Select((column, i) =>
                string.Join('|', column, fk.PrincipalTable, fk.PrincipalColumns![i], Action(fk.OnUpdate), Action(fk.OnDelete))));
            if (!actualForeignKeys.Order(StringComparer.Ordinal).SequenceEqual(expectedForeignKeys.Order(StringComparer.Ordinal)))
                throw new InvalidOperationException($"SQLite foreign keys for {table.Name} are missing or incompatible.");
        }
        var existing = await ExistingTables();
        var missing = tables.Where(x => !existing.Contains(x.Name)).Select(x => x.Name).ToHashSet();
        if ((!initialize && missing.Count > 0) || (existing.Count > 0 && missing.Any(x => !AddedTables.Contains(x))))
            throw new InvalidOperationException("SQLite schema is missing required legacy tables or initialization is disabled. No automatic legacy repair is supported.");
        foreach (var table in tables.Where(x => existing.Contains(x.Name))) await Validate(table);
        await using (var integrity = connection.CreateCommand())
        {
            integrity.Transaction = transaction.GetDbTransaction(); integrity.CommandText = "PRAGMA foreign_key_check";
            await using var reader = await integrity.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) throw new InvalidOperationException("SQLite data violates foreign keys. No automatic data repair is supported.");
        }
        var additions = operations.Where(x => x switch
        {
            CreateTableOperation table => missing.Contains(table.Name),
            CreateIndexOperation index => missing.Contains(index.Table),
            InsertDataOperation seed => missing.Contains(seed.Table),
            _ => false
        }).ToArray();
        foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(additions, model))
        {
            if (sql.TransactionSuppressed) throw new InvalidOperationException("A local G1 upgrade must be transactional.");
            await db.Database.ExecuteSqlRawAsync(sql.CommandText, ct);
        }
        foreach (var table in tables) await Validate(table);
        await transaction.CommitAsync(ct);
    }

    private static string Action(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE", ReferentialAction.Restrict => "RESTRICT",
        ReferentialAction.SetNull => "SET NULL", ReferentialAction.SetDefault => "SET DEFAULT", _ => "NO ACTION"
    };
}
