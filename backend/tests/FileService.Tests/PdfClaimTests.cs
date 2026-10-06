using Das.PdfProtocol;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace FileService.Tests;

public sealed class PdfClaimTests
{
    internal sealed class Documents : IPdfDocumentClient
    {
        public string State = "Preparing"; public bool Ready; public bool AllowRead; public bool Offline;
        public PdfPrepare Request = null!;
        public Task<PdfOperation> OperationAsync(Guid id,CancellationToken ct) => Offline ? throw new PdfProtocolException(503,"OFFLINE") :
            Task.FromResult(new PdfOperation(id,Request.DocumentId,Request.FileId,State,Ready,Request.UploaderUserId,Request.ExpectedVersion));
        public Task<bool> CanReadAsync(Guid d,Guid f,Guid o,Guid u,CancellationToken ct) => Offline ? throw new PdfProtocolException(503,"OFFLINE") : Task.FromResult(AllowRead);
    }
    internal static PdfClaims Service(UploadSecurityTests.Fixture f, Documents d) => new(f.Db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=f.Root}).Build(),d,TimeProvider.System);
    [Fact]
    public async Task Prepare_binds_owned_verified_file_and_replay_is_identical()
    {
        await using var f=await UploadSecurityTests.Fixture.Create(); var user=Guid.NewGuid();
        var file=await f.Service(scanner:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),user);
        var d=new Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,user,1)};var s=Service(f,d);
        var receipt=await s.PrepareAsync(d.Request);Assert.Equal(receipt,await s.PrepareAsync(d.Request));
        Assert.Equal("Prepared",receipt.State);Assert.Equal(d.Request.DocumentId,(await f.Db.Set<PdfUpload>().SingleAsync()).DocumentId);
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().GetFileInfoAsync(file.Id,user))).Status);
    }
    [Theory][InlineData("foreign")][InlineData("pending")][InlineData("tampered")]
    public async Task Unverified_or_foreign_upload_never_becomes_prepared(string mode)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var user=Guid.NewGuid();
        var file=await f.Service(scanner:mode=="pending"?null:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),user);
        if(mode=="tampered")await File.WriteAllTextAsync(file.StoragePath,"bad bytes");
        var d=new Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,mode=="foreign"?Guid.NewGuid():user,1)};
        await Assert.ThrowsAsync<FileRuleException>(()=>Service(f,d).PrepareAsync(d.Request));
        Assert.Empty(await f.Db.Set<PdfClaim>().ToListAsync());Assert.Null((await f.Db.Set<PdfUpload>().SingleAsync()).DocumentId);
    }
    [Fact]
    public async Task Activate_requires_committed_matching_current_document_proof()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var user=Guid.NewGuid();var file=await f.Service(scanner:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),user);
        var d=new Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,user,1)};var s=Service(f,d);await s.PrepareAsync(d.Request);
        await Assert.ThrowsAsync<FileRuleException>(()=>s.ActivateAsync(d.Request.OperationId));
        d.State="Desired";Assert.Equal("Active",(await s.ActivateAsync(d.Request.OperationId)).State);
        Assert.Equal("Active",(await s.ActivateAsync(d.Request.OperationId)).State);
    }
    [Fact]
    public async Task Retirement_preserves_old_bytes_until_new_current_ready_then_deletes_idempotently()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var user=Guid.NewGuid();var file=await f.Service(scanner:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),user);
        var d=new Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,user,1)};var s=Service(f,d);await s.PrepareAsync(d.Request);
        d.State="Desired";await s.ActivateAsync(d.Request.OperationId);d.State="Superseded";
        await Assert.ThrowsAsync<FileRuleException>(()=>s.RetireAsync(d.Request.OperationId));Assert.True(File.Exists(file.StoragePath));
        d.Ready=true;await s.RetireAsync(d.Request.OperationId);await s.RetireAsync(d.Request.OperationId);
        Assert.False(File.Exists(file.StoragePath));Assert.Equal("Deleted",(await f.Db.Set<PdfClaim>().SingleAsync()).State);
        Assert.Single(await f.Db.Files.ToListAsync()); // Audit metadata remains; bytes history does not.
    }
}
