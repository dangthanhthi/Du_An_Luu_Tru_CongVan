using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace PartnerService;

public sealed record PartnerDatabaseOptions(string Provider, string ConnectionString, bool Initialize, bool SeedExamples)
{
    public static PartnerDatabaseOptions Read(IConfiguration config, bool development)
    {
        var connection = config.GetConnectionString("Default");
        var provider = config["Database:Provider"];
        if (development)
        {
            provider ??= string.IsNullOrWhiteSpace(connection) || connection.Contains(".db", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : "SqlServer";
            if (string.IsNullOrWhiteSpace(connection) && provider == "Sqlite") connection = "Data Source=partner_local.db";
        }
        var initialize = Flag("Database:InitializeOnStartup"); var seed = Flag("Database:SeedExamples");
        if (provider is not ("SqlServer" or "Sqlite") || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure Database:Provider and ConnectionStrings:Default explicitly.");
        if (!development && (provider != "SqlServer" || initialize || seed))
            throw new InvalidOperationException("Production Database must use explicit SqlServer with initialization and seeding disabled. Apply reviewed migrations separately.");
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
        return new(provider, connection, initialize, seed);

        bool Flag(string name)
        {
            var text = config[name]; if (text is null) return false;
            if (bool.TryParse(text, out var value)) return value;
            throw new InvalidOperationException(name + " must be true or false.");
        }
    }
}
