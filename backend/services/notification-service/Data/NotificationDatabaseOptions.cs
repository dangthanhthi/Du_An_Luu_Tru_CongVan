using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace NotificationService.Data;

public sealed record NotificationDatabaseOptions(string Provider, string ConnectionString, bool Initialize)
{
    public static NotificationDatabaseOptions Read(IConfiguration config, bool development)
    {
        var connection = config.GetConnectionString("Default"); var provider = config["Database:Provider"];
        if (development)
        {
            provider ??= string.IsNullOrWhiteSpace(connection) || connection.Contains(".db", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : "SqlServer";
            if (string.IsNullOrWhiteSpace(connection) && provider == "Sqlite") connection = "Data Source=notification_local.db";
        }
        var text = config["Database:Initialize"];
        if (text is not null && !bool.TryParse(text, out _)) throw new InvalidOperationException("Database:Initialize must be true or false.");
        var initialize = text is not null && bool.Parse(text);
        if (provider is not ("SqlServer" or "Sqlite") || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure Database:Provider and ConnectionStrings:Default explicitly.");
        if (!development && (provider != "SqlServer" || initialize))
            throw new InvalidOperationException("Production Database must use explicit SqlServer with initialization disabled. Apply reviewed migrations separately.");
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
        catch (ArgumentException) { throw new InvalidOperationException("ConnectionStrings:Default does not match the configured Database provider."); }
        return new(provider, connection, initialize);
    }
}
