using Das.PdfProtocol;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace FileService.Tests;
public sealed class PdfClaimSqlTests
{
    [FileSqlFact]
    public async Task Many_claims_for_same_file_have_only_one_winner_and_preserve_upload()
    {
        await using var f=await FileSqlTests.Fixture.Create();var actor=Guid.NewGuid();Guid file;
        await using(var db=f.Db())file=(await f.Service(db).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor)).Id;
        var results=await Task.WhenAll(Enumerable.Range(0,16).Select(async _=>{
            await using var db=f.Db();var documents=new PdfClaimTests.Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file,actor,1)};
            try {await new PdfClaims(db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=f.Root}).Build(),documents,TimeProvider.System).PrepareAsync(documents.Request);return 200;}
            catch(FileRuleException e){return e.Status;}
        }));
        Assert.Equal(1,results.Count(x=>x==200));Assert.Equal(15,results.Count(x=>x==409));await using var check=f.Db();Assert.Single(await check.Set<PdfClaim>().ToListAsync());Assert.Equal("Available",(await check.Set<PdfUpload>().SingleAsync()).State);
    }
    [FileSqlFact]
    public async Task Additive_claim_migration_preserves_managed_upload_and_enforces_single_file_claim()
    {
        await using var f=await FileSqlTests.Fixture.Create("20261004142642_AddManagedPdfUploads");await using var db=f.Db();var actor=Guid.NewGuid();var file=await f.Service(db).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);
        var hash=(await db.Set<PdfUpload>().SingleAsync()).Sha256;await db.Database.MigrateAsync();db.ChangeTracker.Clear();Assert.Equal(hash,(await db.Set<PdfUpload>().SingleAsync()).Sha256);
        var first=new PdfClaim{OperationId=Guid.NewGuid(),FileId=file.Id,DocumentId=Guid.NewGuid(),UploaderUserId=actor,ExpectedVersion=1,CreatedAt=DateTimeOffset.UtcNow};db.Add(first);await db.SaveChangesAsync();
        db.Add(new PdfClaim{OperationId=Guid.NewGuid(),FileId=file.Id,DocumentId=Guid.NewGuid(),UploaderUserId=actor,ExpectedVersion=1,CreatedAt=DateTimeOffset.UtcNow});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();Assert.Single(await db.Set<PdfClaim>().ToListAsync());Assert.True(File.Exists(file.StoragePath));
    }
}
