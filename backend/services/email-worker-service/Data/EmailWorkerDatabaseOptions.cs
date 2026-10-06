using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EmailWorkerService.Data;

public sealed record EmailWorkerDatabaseOptions(string Provider, string ConnectionString, bool Initialize)
{
    public static EmailWorkerDatabaseOptions Read(IConfiguration configuration, bool development)
    {
        var provider = configuration["Database:Provider"];
        var connection = configuration.GetConnectionString("Default");
        if (provider is not ("SqlServer" or "Sqlite") || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure Database:Provider and ConnectionStrings:Default explicitly.");
        var initialize = configuration.GetValue<bool>("Database:Initialize");
        if (!development && (provider == "Sqlite" || initialize))
            throw new InvalidOperationException("SQLite and startup schema changes are limited to Development. Provision the SQL schema separately before startup.");
        try
        {
            if (provider == "Sqlite")
            {
                if (string.IsNullOrWhiteSpace(new SqliteConnectionStringBuilder(connection).DataSource)) throw new ArgumentException();
            }
            else
            {
                var parsed = new SqlConnectionStringBuilder(connection);
                if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog)) throw new ArgumentException();
            }
        }
        catch (ArgumentException) { throw new InvalidOperationException("The connection string does not match the selected database provider."); }
        return new(provider, connection, initialize);
    }

    public async Task VerifyAsync(EmailWorkerDbContext db, bool development, CancellationToken cancellationToken = default)
    {
        if (!development && (Provider == "Sqlite" || Initialize))
            throw new InvalidOperationException("Startup schema changes and SQLite are limited to Development.");
        if (db.Database.IsSqlServer())
        {
            if (db.Database.HasPendingModelChanges())
                throw new InvalidOperationException("Email database model differs from its migration snapshot. Generate and review a migration before startup.");
            if (Initialize)
                await db.Database.MigrateAsync(cancellationToken);
            else if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                throw new InvalidOperationException("Apply reviewed email SQL migrations separately before startup. Existing EnsureCreated databases require an audited baseline.");
        }
        else if (Initialize) await db.Database.EnsureCreatedAsync(cancellationToken);
        // No schema writes when initialization is disabled; missing tables fail startup.
        await db.EmailImapSettings.AsNoTracking().Take(1).ToListAsync(cancellationToken);
        await db.EmailScanLogs.AsNoTracking().Take(1).ToListAsync(cancellationToken);
        await db.EmailScanItemLogs.AsNoTracking().Take(1).ToListAsync(cancellationToken);
    }
}
