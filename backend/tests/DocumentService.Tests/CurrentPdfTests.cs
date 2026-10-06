using Das.PdfProtocol;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;

public sealed class CurrentPdfTests
{
    internal sealed class Files : IPdfFilesClient
    {
        public bool Offline; public bool ActivationOffline;
        public Dictionary<Guid,PdfReceipt> Receipts = new(); public HashSet<Guid> Retired = [];
        public Task<PdfReceipt> PrepareAsync(PdfPrepare r,CancellationToken ct) {
            if(Offline)throw new PdfProtocolException(503,"OFFLINE");
            var value=new PdfReceipt(r.OperationId,r.DocumentId,r.FileId,r.UploaderUserId,"test.pdf",123,new string('A',64),"Prepared");
            Receipts[r.OperationId]=value;return Task.FromResult(value); }
        public Task<PdfReceipt> ActivateAsync(Guid id,CancellationToken ct) => ActivationOffline ? throw new PdfProtocolException(503,"OFFLINE") : Task.FromResult(Receipts[id] with{State="Active"});
        public Task RetireAsync(Guid id,CancellationToken ct){Retired.Add(id);return Task.CompletedTask;}
        public Task<PdfReceipt> InspectAsync(Guid id,CancellationToken ct)=>Task.FromResult(Receipts[id] with{State="Active"});
    }
    internal static PdfReplaceDraft Draft(Document doc) => new(Guid.NewGuid(),Guid.NewGuid(),doc.Registration!.Version);
    [Fact]
    public async Task Originator_replacement_updates_last_editor_and_preserves_original_inputter_and_number()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new Files();var actor=V2EditingTests.Actor(V2PersistenceTests.Identity.OriginatorUserId);
        await new CurrentPdfService(f.Db,files,V2EditingTests.Clock).ReplaceAsync(doc.Id,Draft(doc),actor);
        var h=await f.Db.DocumentRegistrations.SingleAsync();Assert.Equal(actor.UserId,h.LastModifierUserId);Assert.Equal(V2PersistenceTests.Identity.InputterUserId,h.InputterUserId);
        Assert.Equal("27-01-0001/HL/ADM",(await f.Db.Documents.SingleAsync()).DocumentNumber);Assert.Equal(2,h.Version);
    }
    [Fact]
    public async Task Replacement_has_one_current_link_versions_editor_audit_event_and_idempotent_receipt()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new Files();var s=new CurrentPdfService(f.Db,files,V2EditingTests.Clock);var draft=Draft(doc);
        var result=await s.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor());Assert.Equal(2,result.Version);
        Assert.Equal(result,await s.ReplaceAsync(doc.Id,draft,V2EditingTests.Actor()));
        Assert.Single(await f.Db.Set<DocumentCurrentPdf>().ToListAsync());Assert.Single(await f.Db.Set<PdfReplacement>().ToListAsync());
        Assert.Single(await f.Db.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(V2EditingTests.Actor().UserId,(await f.Db.DocumentRegistrations.SingleAsync()).LastModifierUserId);
        Assert.Equal("Pending",(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).State);
        await s.DispatchAsync();Assert.Equal("Ready",(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).State);
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }
    [Theory][InlineData("inactive")][InlineData("foreign")][InlineData("stale")]
    public async Task Unauthorized_or_stale_requests_do_not_claim_remote_file(string mode)
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new Files();var actor=mode=="inactive"?V2EditingTests.Actor() with{IsActive=false}:mode=="foreign"?V2EditingTests.Actor(Guid.NewGuid()):V2EditingTests.Actor();
        var draft=Draft(doc) with{ExpectedVersion=mode=="stale"?2:1};
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new CurrentPdfService(f.Db,files,V2EditingTests.Clock).ReplaceAsync(doc.Id,draft,actor));
        Assert.Contains(e.Status,new[]{403,409});Assert.Empty(files.Receipts);Assert.Empty(await f.Db.Set<DocumentCurrentPdf>().ToListAsync());
    }
    [Fact]
    public async Task Activation_failure_remains_pending_and_later_dispatch_recovers_without_new_version()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new Files{ActivationOffline=true};var s=new CurrentPdfService(f.Db,files,V2EditingTests.Clock);
        await s.ReplaceAsync(doc.Id,Draft(doc),V2EditingTests.Actor());await s.DispatchAsync();Assert.Equal("Pending",(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).State);
        files.ActivationOffline=false;await s.DispatchAsync();Assert.Equal("Ready",(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).State);Assert.Equal(2,(await f.Db.DocumentRegistrations.SingleAsync()).Version);
    }
    [Fact]
    public async Task Old_claim_is_retired_only_after_replacement_is_ready_and_never_returned_as_current()
    {
        await using var f=await V2EditingTests.Fixture.Create();var doc=await f.Register();var files=new Files();var s=new CurrentPdfService(f.Db,files,V2EditingTests.Clock);var old=Draft(doc);
        await s.ReplaceAsync(doc.Id,old,V2EditingTests.Actor());await s.DispatchAsync();doc=await f.Db.Documents.Include(x=>x.Registration).SingleAsync();var next=Draft(doc);
        await s.ReplaceAsync(doc.Id,next,V2EditingTests.Actor());Assert.Equal("Superseded",(await s.OperationAsync(old.OperationId)).State);Assert.Empty(files.Retired);
        await s.DispatchAsync();await s.DispatchAsync();Assert.Contains(old.OperationId,files.Retired);Assert.Equal(next.FileId,(await f.Db.Set<DocumentCurrentPdf>().SingleAsync()).FileId);
    }
}
