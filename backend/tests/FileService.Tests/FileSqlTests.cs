using FilesService.Data;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace FileService.Tests;

public sealed class FileSqlFactAttribute:FactAttribute
{
    public FileSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))Skip="Requires isolated SQL Server fixture; run scripts/qa/run-file-sql-tests.ps1.";}
}
public sealed class FileSqlTests
{
    [FileSqlFact]
    public async Task Additive_migration_preserves_historical_file_metadata_and_never_marks_it_managed()
    {
        await using var f=await Fixture.Create("20260806035121_InitialFileSchema");await using var db=f.Db();var actor=Guid.NewGuid();var legacy=new FileRecord{Id=Guid.NewGuid(),OriginalName="Historical invoice",ContentType="old/pdf",SizeBytes=987,StoragePath="C:/legacy-do-not-rewrite.pdf",UploadedByUserId=actor,CreatedAt=new(2020,1,1)};
        db.Files.Add(legacy);await db.SaveChangesAsync();await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var saved=await db.Files.SingleAsync();Assert.Equal(legacy.Id,saved.Id);Assert.Equal(legacy.StoragePath,saved.StoragePath);Assert.Equal(987,saved.SizeBytes);Assert.Empty(await db.Set<PdfUpload>().ToListAsync());
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service(db).GetFileInfoAsync(saved.Id,actor))).Status);
        var uploaded=await f.Service(db).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);Assert.NotEqual(saved.Id,uploaded.Id);Assert.Equal(2,await db.Files.CountAsync());Assert.Single(await db.Set<PdfUpload>().ToListAsync());
    }
    [FileSqlFact]
    public async Task Real_file_metadata_constraint_failure_retains_receiving_intent_and_final_bytes()
    {
        await using var f=await Fixture.Create();await using var db=f.Db();await db.Database.ExecuteSqlRawAsync("ALTER TABLE [files].[Files] ADD CONSTRAINT [CK_QA_RejectFiles] CHECK ([SizeBytes] = -1)");
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Service(db).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),Guid.NewGuid()));
        Assert.Empty(await db.Files.ToListAsync());var ticket=await db.Set<PdfUpload>().SingleAsync();Assert.Equal("Receiving",ticket.State);Assert.Equal(1,ticket.Version);
        Assert.True(File.Exists(Path.Combine(f.Root,ticket.StorageKey)));Assert.Single(Directory.GetFiles(f.Root));
    }
    [FileSqlFact]
    public async Task Concurrent_uploads_with_same_display_name_preserve_distinct_server_keys_and_owner_scope()
    {
        await using var f=await Fixture.Create();var actor=Guid.NewGuid();var pdf=PdfStorageTests.Pdf();
        var ids=await Task.WhenAll(Enumerable.Range(0,8).Select(async _=>{await using var db=f.Db();return (await f.Service(db).UploadFileAsync(UploadSecurityTests.Form(pdf,"Same.pdf"),actor)).Id;}));
        Assert.Equal(8,ids.Distinct().Count());await using var check=f.Db();var tickets=await check.Set<PdfUpload>().ToListAsync();Assert.Equal(8,tickets.Count);Assert.Equal(8,await check.Files.CountAsync());Assert.Equal(8,Directory.GetFiles(f.Root).Length);
        Assert.All(tickets,x=>Assert.Equal("Available",x.State));
        var service=f.Service(check);Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>service.GetFileInfoAsync(ids[0],Guid.NewGuid()))).Status);
        var response=await service.DownloadFileAsync(ids[0],actor);await using var stream=response.fileStream;using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);Assert.Equal(pdf,bytes.ToArray());
    }
    [FileSqlFact]
    public async Task Pending_scan_state_and_constraints_survive_fresh_sql_context()
    {
        await using var f=await Fixture.Create();Guid id;var actor=Guid.NewGuid();
        await using(var db=f.Db())id=(await f.Service(db,false).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor)).Id;
        await using var check=f.Db();Assert.Equal("PendingScan",(await f.Service(check).GetFileInfoAsync(id,actor)).State);
        Assert.Equal(423,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service(check).DownloadFileAsync(id,actor))).Status);
        var ticket=await check.Set<PdfUpload>().SingleAsync();ticket.State="ForgedReady";
        await Assert.ThrowsAsync<DbUpdateException>(()=>check.SaveChangesAsync());check.ChangeTracker.Clear();Assert.Equal("PendingScan",(await check.Set<PdfUpload>().SingleAsync()).State);
    }
    internal sealed class Fixture:IAsyncDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"das-file-sql-"+Guid.NewGuid().ToString("N"));
        private readonly string name="das_file_qa_"+Guid.NewGuid().ToString("N");private readonly string master=Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        public FileDbContext Db()=>new(new DbContextOptionsBuilder<FileDbContext>().UseSqlServer(new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString,sql=>sql.EnableRetryOnFailure(5,TimeSpan.FromMilliseconds(200),null)).Options);
        public FileStorageService Service(FileDbContext db,bool clean=true)=>new(db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=Root}).Build(),clean?new PdfStorageTests.Scanner():null);
        public static async Task<Fixture> Create(string? migration=null){var f=new Fixture();Directory.CreateDirectory(f.Root);try{await using var c=new SqlConnection(f.master);await c.OpenAsync();await using var command=c.CreateCommand();command.CommandText="CREATE DATABASE ["+f.name+"]";await command.ExecuteNonQueryAsync();await using var db=f.Db();await db.GetService<IMigrator>().MigrateAsync(migration);return f;}catch{await f.DisposeAsync();throw;}}
        public async ValueTask DisposeAsync(){SqlConnection.ClearAllPools();await using var c=new SqlConnection(master);await c.OpenAsync();await using var command=c.CreateCommand();command.CommandText="IF DB_ID('"+name+"') IS NOT NULL BEGIN ALTER DATABASE ["+name+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+name+"]; END";await command.ExecuteNonQueryAsync();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
}
