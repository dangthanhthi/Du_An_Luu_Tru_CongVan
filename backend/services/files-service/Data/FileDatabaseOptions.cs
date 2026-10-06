using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
namespace FilesService.Data;
public sealed record FileDatabaseOptions(string Provider, string ConnectionString, bool Initialize)
{
    public static FileDatabaseOptions Read(IConfiguration config, bool development)
    {
        var provider=config["Database:Provider"];var connection=config.GetConnectionString("Default");
        if(provider is not ("SqlServer" or "Sqlite") || string.IsNullOrWhiteSpace(connection)) throw Invalid();
        var raw=config["Database:Initialize"];
        if(raw is not null && !bool.TryParse(raw,out _)) throw Invalid();
        var initialize=raw is null?development:bool.Parse(raw);
        if(!development && (provider=="Sqlite" || initialize)) throw Invalid();
        try {
            if(provider=="Sqlite") { if(string.IsNullOrWhiteSpace(new SqliteConnectionStringBuilder(connection).DataSource))throw Invalid(); }
            else { var parsed=new SqlConnectionStringBuilder(connection);if(string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog))throw Invalid(); }
        } catch(ArgumentException){throw Invalid();}
        return new(provider,connection,initialize);
    }
    private static InvalidOperationException Invalid()=>new("Configure Database:Provider, matching ConnectionStrings:Default and boolean Database:Initialize. Production requires SQL Server without startup schema changes.");
}
