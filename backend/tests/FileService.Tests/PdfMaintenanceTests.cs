using System.Data.Common;
using Das.PdfProtocol;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace FileService.Tests;
public sealed class PdfMaintenanceTests
{
    private static IConfiguration Config(UploadSecurityTests.Fixture f)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=f.Root}).Build();
    [Fact]
    public async Task Stalled_final_bytes_recover_to_pending_scan_without_faking_clean_and_keep_uploader_provenance()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var id=Guid.NewGuid();var actor=Guid.NewGuid();f.Db.Add(new PdfUpload{FileId=id,StorageKey=id.ToString("N")+".pdf",UploaderUserId=actor,OriginalName="test.pdf",CreatedAt=DateTimeOffset.UtcNow.AddDays(-2),UpdatedAt=DateTimeOffset.UtcNow.AddDays(-2)});await f.Db.SaveChangesAsync();await File.WriteAllBytesAsync(Path.Combine(f.Root,id.ToString("N")+".pdf"),PdfStorageTests.Pdf());
        await new PdfUploadMaintenance(f.Db,Config(f),new UnavailablePdfThreatScanner(),TimeProvider.System).RunAsync();
        var info=await f.Service().GetFileInfoAsync(id,actor);Assert.Equal("PendingScan",info.State);Assert.False(info.CanDownload);Assert.Single(await f.Db.Files.ToListAsync());Assert.Equal(64,info.Sha256.Length);
        await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().DownloadFileAsync(id,actor));
    }
    [Theory][InlineData("clean")][InlineData("malicious")][InlineData("unavailable")]
    public async Task Rescan_only_trusted_clean_becomes_available_and_rejected_bytes_are_removed(string mode)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var file=await f.Service().UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);
        var scanner=new Scanner(mode=="clean"?PdfScanResult.Clean:mode=="malicious"?PdfScanResult.Malicious:PdfScanResult.Unavailable);
        await new PdfUploadMaintenance(f.Db,Config(f),scanner,TimeProvider.System).RunAsync();
        var saved=await f.Db.Set<PdfUpload>().AsNoTracking().SingleAsync();Assert.Equal(mode=="clean"?"Available":mode=="malicious"?"Rejected":"PendingScan",saved.State);
        Assert.Equal(mode!="malicious",File.Exists(file.StoragePath));
    }
    [Fact]
    public async Task Cleanup_never_walks_legacy_paths_or_removes_recent_uploads()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var old=Guid.NewGuid();var recent=Guid.NewGuid();
        foreach(var id in new[]{old,recent}){f.Db.Add(new PdfUpload{FileId=id,StorageKey=id.ToString("N")+".pdf",OriginalName="test.pdf",UploaderUserId=Guid.NewGuid(),CreatedAt=DateTimeOffset.UtcNow.AddHours(id==old?-48:-1)});await File.WriteAllTextAsync(Path.Combine(f.Root,id.ToString("N")+".upload"),"partial");}
        await f.Db.SaveChangesAsync();await File.WriteAllTextAsync(Path.Combine(f.Root,"unknown-legacy.pdf"),"legacy bytes");
        await new PdfUploadMaintenance(f.Db,Config(f),new UnavailablePdfThreatScanner(),TimeProvider.System).RunAsync();
        Assert.False(File.Exists(Path.Combine(f.Root,old.ToString("N")+".upload")));Assert.True(File.Exists(Path.Combine(f.Root,recent.ToString("N")+".upload")));Assert.True(File.Exists(Path.Combine(f.Root,"unknown-legacy.pdf")));
    }
    [Theory][InlineData("prepare")][InlineData("activate")][InlineData("retire")]
    public async Task Lost_actual_claim_commit_acknowledgement_replays_without_duplicate_claim_or_wrong_delete(string boundary)
    {
        var fault=new LostCommit();await using var f=await UploadSecurityTests.Fixture.Create(fault,retry:true);var actor=Guid.NewGuid();var file=await f.Service(scanner:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);
        var docs=new PdfClaimTests.Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,actor,1)};var s=PdfClaimTests.Service(f,docs);
        if(boundary=="prepare")fault.Enabled=true;await s.PrepareAsync(docs.Request);docs.State="Desired";
        if(boundary=="activate")fault.Enabled=true;await s.ActivateAsync(docs.Request.OperationId);
        if(boundary=="retire"){docs.State="Superseded";docs.Ready=true;fault.Enabled=true;await s.RetireAsync(docs.Request.OperationId);}
        Assert.Equal(1,fault.Thrown);Assert.Single(await f.Db.Set<PdfClaim>().ToListAsync());Assert.Equal(boundary!="retire",File.Exists(file.StoragePath));
    }
    [Fact]
    public async Task Bound_access_rechecks_document_authority_for_owner_and_foreign_reader()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var file=await f.Service(scanner:new PdfStorageTests.Scanner()).UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);
        var docs=new PdfClaimTests.Documents{Request=new(Guid.NewGuid(),Guid.NewGuid(),file.Id,actor,1)};var s=PdfClaimTests.Service(f,docs);await s.PrepareAsync(docs.Request);docs.State="Desired";await s.ActivateAsync(docs.Request.OperationId);
        var storage=new FileStorageService(f.Db,Config(f),documents:docs);Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>storage.GetFileInfoAsync(file.Id,actor))).Status);
        docs.AllowRead=true;var info=await storage.GetFileInfoAsync(file.Id,Guid.NewGuid());Assert.True(info.CanDownload);Assert.False(info.CanAttach);
        docs.AllowRead=false;Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>storage.GetFileInfoAsync(file.Id,actor))).Status);
        docs.Offline=true;Assert.Equal(503,(await Assert.ThrowsAsync<FileRuleException>(()=>storage.GetFileInfoAsync(file.Id,actor))).Status);
    }
    private sealed class Scanner(PdfScanResult result):IPdfThreatScanner{public Task<PdfScanResult> ScanAsync(Stream s,CancellationToken ct)=>Task.FromResult(result);}
    [Fact]
    public async Task Losing_scan_cas_must_not_delete_bytes_after_another_worker_made_the_file_available()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var file=await f.Service().UploadFileAsync(UploadSecurityTests.Form(PdfStorageTests.Pdf()),actor);
        await new PdfUploadMaintenance(f.Db,Config(f),new ConcurrentClean(f.Db),TimeProvider.System).RunAsync();
        Assert.True(File.Exists(file.StoragePath));Assert.Equal("Available",(await f.Db.Set<PdfUpload>().AsNoTracking().SingleAsync()).State);
        Assert.True((await f.Service().GetFileInfoAsync(file.Id,actor)).CanDownload);
    }
    private sealed class ConcurrentClean(FilesService.Data.FileDbContext db):IPdfThreatScanner {
        public async Task<PdfScanResult> ScanAsync(Stream s,CancellationToken ct){await db.Set<PdfUpload>().Where(x=>x.State=="PendingScan").ExecuteUpdateAsync(set=>set.SetProperty(x=>x.State,"Available").SetProperty(x=>x.Version,x=>x.Version+1),ct);return PdfScanResult.Malicious;} }
    private sealed class LostCommit:DbTransactionInterceptor{public bool Enabled;public int Thrown;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken ct=default){if(Enabled){Enabled=false;Thrown++;throw new TimeoutException("Injected actual claim commit ACK loss");}return Task.CompletedTask;}}
}
