using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
namespace FilesService.Data;

public static class FileSqliteUpgrade
{
    public static async Task ApplyAsync(FileDbContext db,bool initialize,CancellationToken ct=default)
    {
        if(!db.Database.IsSqlite())throw new InvalidOperationException("Local file upgrade requires SQLite.");
        var model=db.GetService<IDesignTimeModel>().Model;
        var ops=db.GetService<IMigrationsModelDiffer>().GetDifferences(null,model.GetRelationalModel());
        var tables=ops.OfType<CreateTableOperation>().ToArray();var indexes=ops.OfType<CreateIndexOperation>().ToArray();
        var keys=tables.ToDictionary(x=>x.Name,x=>x.PrimaryKey!.Columns.ToArray());
        await db.Database.OpenConnectionAsync(ct);await using var tx=await db.Database.BeginTransactionAsync(ct);
        var connection=db.Database.GetDbConnection();await using var command=connection.CreateCommand();command.Transaction=tx.GetDbTransaction();
        command.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";
        var existing=new HashSet<string>();await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))existing.Add(reader.GetString(0));
        var missing=tables.Where(x=>!existing.Contains(x.Name)).Select(x=>x.Name).ToHashSet();
        if((!initialize && missing.Count>0) || existing.Any(x=>!tables.Any(t=>t.Name==x)) || (existing.Count>0 && !existing.Contains("Files")))throw Invalid();
        async Task Validate(CreateTableOperation table) {
            command.CommandText=$"PRAGMA table_info(\"{table.Name}\")";var columns=new Dictionary<string,(string Type,bool Nullable,int Key)>();
            await using(var r=await command.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))columns.Add(r.GetString(1),(r.GetString(2),r.GetInt64(3)==0,r.GetInt32(5)));
            if(columns.Count!=table.Columns.Count || table.Columns.Any(x=>!columns.TryGetValue(x.Name,out var actual) || actual.Type!=x.ColumnType || actual.Nullable!=x.IsNullable) ||
                !columns.Where(x=>x.Value.Key>0).OrderBy(x=>x.Value.Key).Select(x=>x.Key).SequenceEqual(keys[table.Name]))throw Invalid();
            command.CommandText=$"PRAGMA index_list(\"{table.Name}\")";var found=new Dictionary<string,bool>();
            await using(var r=await command.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))found[r.GetString(1)]=r.GetInt64(2)!=0;
            foreach(var index in indexes.Where(x=>x.Table==table.Name)) {
                if(!found.TryGetValue(index.Name,out var unique) || unique!=index.IsUnique)throw Invalid();
                command.CommandText=$"PRAGMA index_info(\"{index.Name}\")";var names=new List<string>();
                await using(var r=await command.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))names.Add(r.GetString(2));
                if(!names.SequenceEqual(index.Columns))throw Invalid();
            }
        }
        foreach(var table in tables.Where(x=>existing.Contains(x.Name)))await Validate(table);
        var additions=ops.Where(x=>x is CreateTableOperation t && missing.Contains(t.Name) || x is CreateIndexOperation i && missing.Contains(i.Table)).ToArray();
        foreach(var sql in db.GetService<IMigrationsSqlGenerator>().Generate(additions,model)) {
            if(sql.TransactionSuppressed)throw Invalid();await db.Database.ExecuteSqlRawAsync(sql.CommandText,ct);
        }
        foreach(var table in tables)await Validate(table);await tx.CommitAsync(ct);
    }
    private static InvalidOperationException Invalid()=>new("The file SQLite schema is missing or incompatible. Use a reviewed migration; no legacy table repair is performed.");
}
