using System.Data;
using System.Security.Cryptography;
using FilesService.Data;
using FilesService.Models.Entities;
using FilesService.Storage;
using Microsoft.EntityFrameworkCore;
namespace FilesService.Services;

// Only server-owned managed intents; never walk/delete arbitrary legacy paths.
public sealed class PdfUploadMaintenance(FileDbContext db,IConfiguration config,IPdfThreatScanner scanner,TimeProvider clock)
{
    private readonly PdfStorageOptions storage=PdfStorageOptions.Read(config);
    public async Task RunAsync(CancellationToken ct=default)
    {
        var pending=await db.Set<PdfUpload>().AsNoTracking().Where(x=>x.DocumentId==null &&
            (x.State=="PendingScan" || x.State=="Receiving" || x.State=="Rejected" || x.State=="Failed") && x.FailureCode!="UPLOAD_CLEANED" && x.Version<long.MaxValue)
            .OrderBy(x=>x.Version).ThenBy(x=>x.FileId).Take(100).ToArrayAsync(ct);
        foreach(var intent in pending) {
            try {
                var stale=intent.CreatedAt<clock.GetUtcNow().AddDays(-1);
                if(intent.State=="Receiving" && stale && File.Exists(storage.PathFor(intent.FileId)) && !await db.Files.AnyAsync(x=>x.Id==intent.FileId,ct))
                    await RecoverOrScan(intent,true,ct);
                else if(intent.State=="PendingScan")await RecoverOrScan(intent,false,ct);
                else if(stale && (intent.State=="Rejected" || intent.FailureCode=="UPLOAD_CLEANUP_PENDING" || !await db.Files.AnyAsync(x=>x.Id==intent.FileId,ct))) {
                    // Fence finalization/claim before touching bytes. A stale observation of
                    // Receiving must never delete a file that finalized in the meantime.
                    var fenced=await db.Set<PdfUpload>().Where(x=>x.FileId==intent.FileId && x.Version==intent.Version && x.DocumentId==null &&
                        (x.State=="Rejected" || x.FailureCode=="UPLOAD_CLEANUP_PENDING" || !db.Files.Any(f=>f.Id==x.FileId)))
                        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Failed").SetProperty(x=>x.FailureCode,"UPLOAD_CLEANUP_PENDING")
                            .SetProperty(x=>x.Version,x=>x.Version+1).SetProperty(x=>x.UpdatedAt,clock.GetUtcNow()),ct);
                    if(fenced==0)continue;
                    File.Delete(storage.PathFor(intent.FileId,true));File.Delete(storage.PathFor(intent.FileId));
                    await db.Set<PdfUpload>().Where(x=>x.FileId==intent.FileId && x.Version==intent.Version+1 && x.DocumentId==null && x.FailureCode=="UPLOAD_CLEANUP_PENDING")
                        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Failed").SetProperty(x=>x.FailureCode,"UPLOAD_CLEANED")
                            .SetProperty(x=>x.Version,x=>x.Version+1).SetProperty(x=>x.UpdatedAt,clock.GetUtcNow()),ct);
                }
            } catch(FileRuleException) {
                db.ChangeTracker.Clear();
                await db.Set<PdfUpload>().Where(x=>x.FileId==intent.FileId && x.Version==intent.Version && x.DocumentId==null)
                    .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Rejected").SetProperty(x=>x.FailureCode,"PDF_INVALID").SetProperty(x=>x.Version,x=>x.Version+1),ct);
            } catch(Exception e) when(e is IOException or DbUpdateException){db.ChangeTracker.Clear();}
            finally {
                await db.Set<PdfUpload>().Where(x=>x.FileId==intent.FileId && x.Version==intent.Version && x.DocumentId==null)
                    .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Version,x=>x.Version+1).SetProperty(x=>x.UpdatedAt,clock.GetUtcNow()),ct);
            }
        }
    }
    private async Task RecoverOrScan(PdfUpload intent,bool recover,CancellationToken ct)
    {
        var path=storage.PathFor(intent.FileId);
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length is <1 || stream.Length>storage.MaxBytes)throw new FileRuleException(422,"PDF_INVALID","Invalid recovered bytes.");
        var size=stream.Length;var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct));
        if(!recover && (size!=intent.SizeBytes || hash!=intent.Sha256)) {
            await db.Set<PdfUpload>().Where(x=>x.FileId==intent.FileId && x.Version==intent.Version).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Missing").SetProperty(x=>x.FailureCode,"STORAGE_INTEGRITY").SetProperty(x=>x.Version,x=>x.Version+1),ct);return;
        }
        stream.Position=0;new PdfValidationService().Validate(stream,ct);stream.Position=0;
        PdfScanResult scan;
        try{scan=await scanner.ScanAsync(stream,ct);}catch(OperationCanceledException){throw;}catch{scan=PdfScanResult.Unavailable;}
        var state=scan==PdfScanResult.Clean?"Available":scan==PdfScanResult.Malicious?"Rejected":"PendingScan";
        // No state is made available without a trusted clean scan. Retry receipts are
        // the intent state/version, not an inferred successful client response.
        var applied=await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
            db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var saved=await db.Set<PdfUpload>().SingleAsync(x=>x.FileId==intent.FileId,ct);
            if(saved.Version!=intent.Version || saved.DocumentId is not null){await tx.CommitAsync(ct);return false;}
            if(recover && await db.Files.AnyAsync(x=>x.Id==intent.FileId,ct)){await tx.CommitAsync(ct);return false;}
            if(recover && state!="Rejected")db.Files.Add(new(){Id=intent.FileId,OriginalName=intent.OriginalName,StoragePath=path,ContentType="application/pdf",SizeBytes=size,UploadedByUserId=intent.UploaderUserId,CreatedAt=intent.CreatedAt.UtcDateTime});
            saved.SizeBytes=size;saved.Sha256=hash;saved.State=state;saved.FailureCode=state=="PendingScan"?"SCANNER_UNAVAILABLE":state=="Rejected"?"PDF_REJECTED":null;saved.Version++;saved.UpdatedAt=clock.GetUtcNow();
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
        });
        // Malicious content stays inaccessible immediately; bytes are removed after DB
        // rejection commits. A deletion failure remains a rejected cleanup candidate.
        await stream.DisposeAsync();
        if(applied && state=="Rejected")File.Delete(path);
        db.ChangeTracker.Clear();
    }
}
