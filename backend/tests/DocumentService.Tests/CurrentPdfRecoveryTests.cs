using System.Data.Common;
using Das.PdfProtocol;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
namespace DocumentService.Tests;
public sealed class CurrentPdfRecoveryTests
{
    [Theory][InlineData(1)][InlineData(2)]
    public async Task Lost_actual_commit_ack_at_preparation_and_link_commit_keeps_single_audit_and_receipt(int commit)
    {
        await using var conn=new SqliteConnection("Data Source=:memory:");await conn.OpenAsync();var fault=new LostCommit();
        var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(conn,s=>s.ExecutionStrategy(d=>new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db=new DocumentDbContext(options);await db.Database.EnsureCreatedAsync();var doc=await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"ack-pdf");
        fault.Left=commit;var files=new CurrentPdfTests.Files();var service=new CurrentPdfService(db,files,V2EditingTests.Clock);var draft=CurrentPdfTests.Draft(doc);
        var result=await service.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor());Assert.Equal(2,result.Version);Assert.Equal(1,fault.Thrown);
        Assert.Equal(result,await service.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor()));Assert.Single(await db.Set<DocumentEditAudit>().ToListAsync());Assert.Single(await db.Set<PdfReplacement>().ToListAsync());Assert.Single(await db.Set<DocumentCurrentPdf>().ToListAsync());
    }
    [Fact]
    public async Task Offline_prepare_remains_retryable_then_expires_to_terminal_abort()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new CurrentPdfTests.Files{Offline=true};var s=new CurrentPdfService(f.Db,files,V2EditingTests.Clock);var draft=CurrentPdfTests.Draft(doc);
        await Assert.ThrowsAsync<PdfProtocolException>(()=>s.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor()));Assert.Equal("Preparing",(await f.Db.Set<PdfReplacement>().SingleAsync()).State);Assert.Empty(await f.Db.Set<DocumentCurrentPdf>().ToListAsync());
        var op=await f.Db.Set<PdfReplacement>().SingleAsync();op.CreatedAt=V2EditingTests.Clock.GetUtcNow().UtcDateTime.AddHours(-3);await f.Db.SaveChangesAsync();
        await s.ExpirePreparingAsync();Assert.Equal("Aborted",(await s.OperationAsync(draft.OperationId)).State);files.Offline=false;
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>s.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor()))).Status);Assert.Empty(files.Receipts);
    }
    [Fact]
    public async Task Operation_id_reuse_with_different_file_and_same_file_reuse_are_rejected()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var s=new CurrentPdfService(f.Db,new CurrentPdfTests.Files(),V2EditingTests.Clock);var draft=CurrentPdfTests.Draft(doc);await s.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor());
        foreach(var changed in new[]{draft with{FileId=Guid.NewGuid()},draft with{OperationId=Guid.NewGuid(),ExpectedVersion=2}})
            Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>s.ReplaceAsync(doc.Id,changed,V2EditingTests.Actor()))).Status);
        Assert.Single(await f.Db.Set<DocumentCurrentPdf>().ToListAsync());Assert.Single(await f.Db.Set<PdfReplacement>().ToListAsync());
    }
    [Fact]
    public async Task Fresh_authority_after_remote_prepare_can_revoke_write_before_commit()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new CurrentPdfTests.Files();var s=new CurrentPdfService(f.Db,files,V2EditingTests.Clock,new Revoked());
        Assert.Equal(403,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>s.ReplaceAsync(doc.Id,CurrentPdfTests.Draft(doc),V2EditingTests.Actor()))).Status);
        Assert.Single(files.Receipts);Assert.Empty(await f.Db.Set<DocumentCurrentPdf>().ToListAsync());Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }
    [Fact]
    public async Task Malformed_pdf_outbox_payload_is_counted_without_starving_valid_activation()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var s=new CurrentPdfService(f.Db,new CurrentPdfTests.Files(),V2EditingTests.Clock);
        await s.ReplaceAsync(doc.Id,CurrentPdfTests.Draft(doc),V2EditingTests.Actor());
        f.Db.DocumentOutboxEvents.Add(new(){DocumentId=doc.Id,Type="PdfRetire",AggregateVersion=1,PayloadJson="not-json",CreatedAt=V2EditingTests.Clock.GetUtcNow()});await f.Db.SaveChangesAsync();
        await s.DispatchAsync();Assert.Equal("Ready",(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).State);
        Assert.Equal(1,(await f.Db.DocumentOutboxEvents.SingleAsync(x=>x.Type=="PdfRetire")).Attempts);
    }
    private sealed class Revoked:IPdfAuthority {
        public Task<V2EditorActor> EditorAsync(Guid d,Guid u,CancellationToken ct)=>Task.FromResult(V2EditingTests.Actor(u) with{IsActive=false});
        public Task<bool> CanReadAsync(Guid d,Guid u,CancellationToken ct)=>Task.FromResult(false); }
    private sealed class RetryOnce(ExecutionStrategyDependencies d):ExecutionStrategy(d,1,TimeSpan.Zero){protected override bool ShouldRetryOn(Exception e)=>e is TimeoutException;}
    private sealed class LostCommit:DbTransactionInterceptor{public int Left;public int Thrown;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken ct=default){if(Left>0 && --Left==0){Thrown++;throw new TimeoutException("Injected current PDF actual COMMIT ACK loss");}return Task.CompletedTask;}}
}
