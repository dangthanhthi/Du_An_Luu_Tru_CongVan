using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using Xunit;
namespace FileService.Tests;

public sealed class PdfStorageTests
{
    internal static byte[] Pdf(){var builder=new PdfDocumentBuilder();builder.AddPage(PageSize.A4);return builder.Build();}
    internal sealed class Scanner(PdfScanResult result=PdfScanResult.Clean,bool throws=false):IPdfThreatScanner {
        public Task<PdfScanResult> ScanAsync(Stream stream,CancellationToken ct){ct.ThrowIfCancellationRequested();if(throws)throw new IOException("Scanner unavailable");return Task.FromResult(result);} }
    [Fact]
    public async Task Available_pdf_has_server_key_actual_hash_and_size_and_owner_can_read_same_bytes()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var bytes=Pdf();var actor=Guid.NewGuid();var service=f.Service(scanner:new Scanner());
        var file=UploadSecurityTests.Form(bytes,"../../Báo cáo.PDF");var record=await service.UploadFileAsync(file,actor);
        var ticket=await f.Db.Set<PdfUpload>().SingleAsync();Assert.Equal(record.Id.ToString("N")+".pdf",ticket.StorageKey);
        Assert.Equal("Báo cáo.PDF",record.OriginalName);Assert.Equal(bytes.Length,record.SizeBytes);Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),ticket.Sha256);
        Assert.Equal("Available",ticket.State);Assert.Equal(2,ticket.Version);Assert.Equal(actor,ticket.UploaderUserId);
        Assert.Equal(Path.Combine(f.Root,ticket.StorageKey),Assert.Single(Directory.GetFiles(f.Root)));Assert.False(File.Exists(Path.Combine(f.Root,record.Id.ToString("N")+".upload")));
        var info=await service.GetFileInfoAsync(record.Id,actor);Assert.True(info.CanDownload);
        Assert.DoesNotContain("StoragePath",System.Text.Json.JsonSerializer.Serialize(info));
        var download=await service.DownloadFileAsync(record.Id,actor);await using var stream=download.fileStream;using var copy=new MemoryStream();await stream.CopyToAsync(copy);
        Assert.Equal(bytes,copy.ToArray());Assert.Equal(ticket.Sha256,download.fileHash);
    }
    [Theory]
    [InlineData("default")][InlineData("unavailable")][InlineData("exception")][InlineData("unknownResult")]
    public async Task Missing_or_failed_scanner_preserves_pdf_in_pending_scan_and_never_serves_content(string kind)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();
        var scanner=kind switch {"default"=>null,"exception"=>new Scanner(throws:true),"unknownResult"=>new Scanner((PdfScanResult)99),_=>new Scanner(PdfScanResult.Unavailable)};
        var service=f.Service(scanner:scanner);var record=await service.UploadFileAsync(UploadSecurityTests.Form(Pdf()),actor);
        Assert.Equal("PendingScan",(await service.GetFileInfoAsync(record.Id,actor)).State);Assert.False((await service.GetFileInfoAsync(record.Id,actor)).CanDownload);
        Assert.Equal(423,(await Assert.ThrowsAsync<FileRuleException>(()=>service.DownloadFileAsync(record.Id,actor))).Status);
        Assert.Equal("SCANNER_UNAVAILABLE",(await f.Db.Set<PdfUpload>().SingleAsync()).FailureCode);Assert.Single(Directory.GetFiles(f.Root));
    }
    [Fact]
    public async Task Scanner_rejection_removes_staged_bytes_and_keeps_rejection_intent_without_file_metadata()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var service=f.Service(scanner:new Scanner(PdfScanResult.Malicious));
        Assert.Equal(422,(await Assert.ThrowsAsync<FileRuleException>(()=>service.UploadFileAsync(UploadSecurityTests.Form(Pdf()),Guid.NewGuid()))).Status);
        Assert.Empty(await f.Db.Files.ToListAsync());Assert.Empty(Directory.GetFiles(f.Root));Assert.Equal("Rejected",(await f.Db.Set<PdfUpload>().SingleAsync()).State);
    }
    [Theory]
    [InlineData("foreign")][InlineData("linked")][InlineData("legacy")][InlineData("keyTraversal")]
    public async Task Guessed_ids_and_legacy_or_bound_files_never_grant_uploader_bypass(string kind)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var service=f.Service(scanner:new Scanner());var record=await service.UploadFileAsync(UploadSecurityTests.Form(Pdf()),actor);
        var ticket=await f.Db.Set<PdfUpload>().SingleAsync();
        if(kind=="linked")ticket.DocumentId=Guid.NewGuid();if(kind=="legacy")f.Db.Remove(ticket);if(kind=="keyTraversal")ticket.StorageKey="../outside.pdf";await f.Db.SaveChangesAsync();
        var reader=kind=="foreign"?Guid.NewGuid():actor;
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>service.GetFileInfoAsync(record.Id,reader))).Status);
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>service.DownloadFileAsync(record.Id,reader))).Status);
    }
    [Theory]
    [InlineData("deleted")][InlineData("tampered")]
    public async Task Missing_or_changed_managed_bytes_fail_closed_and_update_state(string kind)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var service=f.Service(scanner:new Scanner());var record=await service.UploadFileAsync(UploadSecurityTests.Form(Pdf()),actor);
        if(kind=="deleted")File.Delete(record.StoragePath);else await File.WriteAllBytesAsync(record.StoragePath,new byte[record.SizeBytes]);
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>service.DownloadFileAsync(record.Id,actor))).Status);
        Assert.Equal("Missing",(await service.GetFileInfoAsync(record.Id,actor)).State);Assert.False((await service.GetFileInfoAsync(record.Id,actor)).CanDownload);
    }
    [Theory]
    [InlineData("beforeFinalSave")][InlineData("afterFinalSave")]
    public async Task Final_database_failure_leaves_durable_receiving_intent_and_final_bytes_for_recovery(string where)
    {
        var fault=new SaveFault(where);await using var f=await UploadSecurityTests.Fixture.Create(fault);fault.Enabled=true;var service=f.Service(scanner:new Scanner());
        await Assert.ThrowsAsync<IOException>(()=>service.UploadFileAsync(UploadSecurityTests.Form(Pdf()),Guid.NewGuid()));
        Assert.Empty(await f.Db.Files.ToListAsync());var ticket=await f.Db.Set<PdfUpload>().SingleAsync();Assert.Equal("Receiving",ticket.State);
        Assert.True(File.Exists(Path.Combine(f.Root,ticket.StorageKey)));Assert.Single(Directory.GetFiles(f.Root));Assert.False(f.Db.ChangeTracker.HasChanges());
    }
    [Fact]
    public async Task Failure_to_persist_intent_never_writes_bytes_and_cleans_unit_of_work()
    {
        var fault=new SaveFault("intent");await using var f=await UploadSecurityTests.Fixture.Create(fault);fault.Enabled=true;
        await Assert.ThrowsAsync<IOException>(()=>f.Service().UploadFileAsync(UploadSecurityTests.Form(Pdf()),Guid.NewGuid()));
        Assert.Empty(Directory.GetFiles(f.Root));Assert.False(f.Db.ChangeTracker.HasChanges());
    }
    private sealed class SaveFault(string where):SaveChangesInterceptor {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken ct=default){
            if(Enabled && (where=="intent" || where=="beforeFinalSave" && e.Context!.ChangeTracker.Entries<FileRecord>().Any(x=>x.State==EntityState.Added)))throw new IOException("Injected save failure");return ValueTask.FromResult(result);}
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e,int result,CancellationToken ct=default){
            if(Enabled && where=="afterFinalSave" && e.Context!.ChangeTracker.Entries<FileRecord>().Any())throw new IOException("Injected post-save failure");return ValueTask.FromResult(result);} }
    [Theory]
    [InlineData(1)][InlineData(2)]
    public async Task Lost_actual_commit_ack_is_recovered_without_duplicate_intent_metadata_or_bytes(int whichCommit)
    {
        var fault=new LostCommit(whichCommit);await using var f=await UploadSecurityTests.Fixture.Create(fault,true);fault.Enabled=true;
        var record=await f.Service(scanner:new Scanner()).UploadFileAsync(UploadSecurityTests.Form(Pdf()),Guid.NewGuid());
        Assert.Single(await f.Db.Files.ToListAsync());Assert.Single(await f.Db.Set<PdfUpload>().ToListAsync());Assert.Single(Directory.GetFiles(f.Root));Assert.Equal(1,fault.Thrown);
        Assert.Equal(record.Id,(await f.Db.Set<PdfUpload>().SingleAsync()).FileId);
    }
    private sealed class LostCommit(int which):DbTransactionInterceptor {
        public bool Enabled;public int Thrown;private int count;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken ct=default){if(Enabled && ++count==which){Enabled=false;Thrown++;throw new TimeoutException("Actual COMMIT lost acknowledgement");}return Task.CompletedTask;} }
    [Fact]
    public async Task Encrypted_pdf_is_rejected_even_when_extension_and_magic_are_valid()
    {
        const string encrypted="JVBERi0xLjMKJeLjz9MKMSAwIG9iago8PAovUHJvZHVjZXIgPDdmZDc2ZTNlZTU+Cj4+CmVuZG9iagoyIDAgb2JqCjw8Ci9UeXBlIC9QYWdlcwovQ291bnQgMQovS2lkcyBbIDQgMCBSIF0KPj4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL0NhdGFsb2cKL1BhZ2VzIDIgMCBSCj4+CmVuZG9iago0IDAgb2JqCjw8Ci9UeXBlIC9QYWdlCi9SZXNvdXJjZXMgPDwKPj4KL01lZGlhQm94IFsgMC4wIDAuMCAxMDAgMTAwIF0KL1BhcmVudCAyIDAgUgo+PgplbmRvYmoKNSAwIG9iago8PAovViAyCi9SIDMKL0xlbmd0aCAxMjgKL1AgNDI5NDk2NzI5MgovRmlsdGVyIC9TdGFuZGFyZAovTyA8YTU3ZWIyYTIzOTQ1NTJlYmM3NWNhMWVjMjA3YzliNmI2ZDhmM2ZlYmY4MWY3ZTQ5NWViNDJlYmFjMjNjZjVjYT4KL1UgPDg4ZjBhMWFmYjA4YWI5OTE1Y2QwMWE4Y2U3ZDE4NTc2MjhiZjRlNWU0ZTc1OGE0MTY0MDA0ZTU2ZmZmYTAxMDg+Cj4+CmVuZG9iagp4cmVmCjAgNgowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMTUgMDAwMDAgbiAKMDAwMDAwMDA1OSAwMDAwMCBuIAowMDAwMDAwMTE4IDAwMDAwIG4gCjAwMDAwMDAxNjcgMDAwMDAgbiAKMDAwMDAwMDI2MSAwMDAwMCBuIAp0cmFpbGVyCjw8Ci9TaXplIDYKL1Jvb3QgMyAwIFIKL0luZm8gMSAwIFIKL0lEIFsgPDMwMzkzNTMzMzE2NjYzMzg2MTM3MzAzMTYzMzczMDYyMzc2MzM3MzIzMzMwMzAzNjM4MzUzNjMyMzYzODY0NjM+IDwzMDM5MzUzMzMxNjY2MzM4NjEzNzMwMzE2MzM3MzA2MjM3NjMzNzMyMzMzMDMwMzYzODM1MzYzMjM2Mzg2NDYzPiBdCi9FbmNyeXB0IDUgMCBSCj4+CnN0YXJ0eHJlZgo0NzYKJSVFT0YK";
        await using var f=await UploadSecurityTests.Fixture.Create();Assert.Equal(422,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().UploadFileAsync(UploadSecurityTests.Form(Convert.FromBase64String(encrypted)),Guid.NewGuid()))).Status);
        Assert.Empty(await f.Db.Files.ToListAsync());Assert.Empty(Directory.GetFiles(f.Root));
    }
    [Fact]
    public async Task Sqlite_additive_upgrade_preserves_legacy_rows_but_does_not_grant_them_managed_access()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var actor=Guid.NewGuid();var legacy=new FileRecord{Id=Guid.NewGuid(),OriginalName="Historical",StoragePath="C:/historical.pdf",ContentType="legacy/pdf",SizeBytes=123,UploadedByUserId=actor};f.Db.Add(legacy);await f.Db.SaveChangesAsync();
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE PdfUploads");await FilesService.Data.FileSqliteUpgrade.ApplyAsync(f.Db,true);await FilesService.Data.FileSqliteUpgrade.ApplyAsync(f.Db,false);
        Assert.Equal("C:/historical.pdf",(await f.Db.Files.SingleAsync()).StoragePath);Assert.Empty(await f.Db.Set<PdfUpload>().ToListAsync());
        Assert.Equal(404,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().GetFileInfoAsync(legacy.Id,actor))).Status);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Exact_configured_byte_boundary_accepts_exactly_the_limit_but_rejects_one_byte_more(bool fits)
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var bytes=Pdf();var service=f.Service(bytes.Length-(fits?0:1));
        if(fits)Assert.Equal(bytes.Length,(await service.UploadFileAsync(UploadSecurityTests.Form(bytes),Guid.NewGuid())).SizeBytes);
        else Assert.Equal(413,(await Assert.ThrowsAsync<FileRuleException>(()=>service.UploadFileAsync(UploadSecurityTests.Form(bytes),Guid.NewGuid()))).Status);
    }
    [Fact]
    public async Task Client_mime_is_not_persisted_as_authoritative_pdf_type()
    {
        await using var f=await UploadSecurityTests.Fixture.Create();var form=(Microsoft.AspNetCore.Http.FormFile)UploadSecurityTests.Form(Pdf());form.ContentType="image/jpeg";
        Assert.Equal("application/pdf",(await f.Service().UploadFileAsync(form,Guid.NewGuid())).ContentType);
    }
}
