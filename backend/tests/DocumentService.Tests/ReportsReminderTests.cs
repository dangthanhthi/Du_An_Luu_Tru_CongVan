using System.IO.Compression;
using System.Xml.Linq;
using Das.PdfProtocol;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReportsReminderTests
{
    private static readonly Guid User=Guid.NewGuid(),Department=Guid.NewGuid();
    private static readonly TimeProvider Clock=new V2PersistenceTests.Clock("2026-10-05T01:00:00Z");
    private static ReportAuthority Scope(bool export=true,bool org=false)=>new(User,true,true,export,org,new HashSet<Guid>{Department},new HashSet<Guid>(),new HashSet<Guid>());
    private static DocumentRegistration Add(DocumentDbContext db,int age=8,string kind="INTERNAL",string status="InProgress",string sensitivity="Normal",Guid? department=null)
    {
        var id=Guid.NewGuid();var doc=new Document{Id=id,DocType=kind,Status=status,Title="Secret subject never projected",DocumentNumber="26-09-"+id,CreatedByUserId=User};
        var sequence=db.ChangeTracker.Entries<DocumentRegistration>().Count()+1;
        var header=new DocumentRegistration{DocumentId=id,Document=doc,Kind=kind,RegistrationDate=new DateOnly(2026,10,5).AddDays(-age),RegistrationYear=2026,SequenceNumber=sequence,OwnerDepartmentId=department??Department,OwnerDepartmentNameSnapshot="ADM",OwnerDepartmentCodeSnapshot="ADM",CompanyCode="HL",CompanyNameSnapshot="Company",InputterUserId=User,OriginatorUserId=User,LastModifierUserId=User,Sensitivity=sensitivity};db.Add(header);return header;
    }
    private static IncompleteReports Service(DocumentDbContext db)=>new(db,new(db,new Files()),Clock);
    [Fact] public async Task Report_scope_is_independent_and_excludes_confidential_cancelled_incoming_recent_complete()
    {
        await using var f=await V2EditingTests.Fixture.Create();Add(f.Db);Add(f.Db,7);Add(f.Db,1);Add(f.Db,kind:"INCOMING");Add(f.Db,status:"Cancelled");Add(f.Db,sensitivity:"Confidential");Add(f.Db,department:Guid.NewGuid());await f.Db.SaveChangesAsync();
        var result=await Service(f.Db).QueryAsync(User,Scope(),new(),1,20,false,default);Assert.Single(result.Items);Assert.Equal(1,result.Groups[0].Count);Assert.Equal(User,result.Items[0].Originator);Assert.DoesNotContain("subject",System.Text.Json.JsonSerializer.Serialize(result),StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3,(await Service(f.Db).QueryAsync(User,Scope(),new(IncludeRecent:true),1,20,false,default)).Total);
    }
    [Fact] public async Task Distributed_without_pdf_is_incomplete_and_missing_dependency_does_not_become_complete()
    {
        await using var f=await V2EditingTests.Fixture.Create();var header=Add(f.Db,status:"Distributed");header.IssuedDate=new(2026,9,1);await f.Db.SaveChangesAsync();Assert.Single((await Service(f.Db).QueryAsync(User,Scope(),new(),1,20,false,default)).Items);
        f.Db.Add(new DocumentCurrentPdf{DocumentId=header.DocumentId,OperationId=Guid.NewGuid(),FileId=Guid.NewGuid(),State="Ready",OriginalName="test.pdf",Sha256=new string('a',64),SizeBytes=100});await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<PdfProtocolException>(()=>Service(f.Db).QueryAsync(User,Scope(),new(),1,20,false,default));
    }
    [Fact] public async Task Export_requires_separate_permission_and_inline_strings_cannot_be_formulas()
    {
        await using var f=await V2EditingTests.Fixture.Create();var h=Add(f.Db);h.OwnerDepartmentNameSnapshot="=HYPERLINK(\"evil\")";await f.Db.SaveChangesAsync();
        var denied=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Service(f.Db).QueryAsync(User,Scope(false),new(),1,20,true,default));Assert.Equal(403,denied.Status);
        var web=await Service(f.Db).QueryAsync(User,Scope(),new(),1,20,false,default);var export=await Service(f.Db).QueryAsync(User,Scope(),new(),1,20,true,default);Assert.Equal(web.Items.Select(x=>x.DocumentId),export.Items.Select(x=>x.DocumentId));
        using var zip=new ZipArchive(new MemoryStream(ReportWorkbook.Create(export)));using var stream=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var xml=XDocument.Load(stream);Assert.Empty(xml.Descendants().Where(x=>x.Name.LocalName=="f"));Assert.Contains(h.OwnerDepartmentNameSnapshot,xml.Value());
    }
    [Fact] public async Task Organization_scope_does_not_grant_confidential()
    {await using var f=await V2EditingTests.Fixture.Create();Add(f.Db,sensitivity:"Confidential");await f.Db.SaveChangesAsync();Assert.Empty((await Service(f.Db).QueryAsync(User,Scope(org:true),new(),1,20,false,default)).Items);}
    [Theory][InlineData(7,false)][InlineData(8,true)]
    public async Task Confidential_warning_uses_same_seven_day_boundary_without_exposing_documents(int age,bool warned)
    {
        await using var f=await V2EditingTests.Fixture.Create();Add(f.Db,age,sensitivity:"Confidential");await f.Db.SaveChangesAsync();
        var svc=new WeeklyReminders(f.Db,Service(f.Db),new Directory(),new UnavailableReminderTransport(),Clock);
        var batch=await svc.PlanAsync(Department);var plan=System.Text.Json.JsonSerializer.Deserialize<ReminderPlan>(batch.PayloadJson)!;
        Assert.Empty(plan.Envelopes);Assert.Equal(warned,plan.Warnings.Count>0);
    }
    [Fact] public async Task Complete_means_active_verified_current_pdf_and_issued_date()
    {
        await using var f=await V2EditingTests.Fixture.Create();var h=Add(f.Db,status:"Distributed");h.IssuedDate=new(2026,9,1);
        var link=new DocumentCurrentPdf{DocumentId=h.DocumentId,OperationId=Guid.NewGuid(),FileId=Guid.NewGuid(),State="Ready",OriginalName="test.pdf",Sha256=new string('a',64),SizeBytes=100};f.Db.Add(link);await f.Db.SaveChangesAsync();
        var remote=new AvailableFiles(new(link.OperationId,h.DocumentId,link.FileId,User,link.OriginalName,link.SizeBytes,link.Sha256,"Active"));var service=new IncompleteReports(f.Db,new(f.Db,remote),Clock);
        Assert.Empty((await service.QueryAsync(User,Scope(),new(),1,20,false,default)).Items);
        remote.Receipt=remote.Receipt with{State="Quarantined"};Assert.Single((await service.QueryAsync(User,Scope(),new(),1,20,false,default)).Items);
        remote.Receipt=remote.Receipt with{Sha256=new string('b',64)};var conflict=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.QueryAsync(User,Scope(),new(),1,20,false,default));Assert.Equal("PDF_RECEIPT_CONFLICT",conflict.Code);
    }
    [Theory][InlineData("2026-10-05T00:59:59Z","2026-09-28")][InlineData("2026-10-05T01:00:00Z","2026-10-05")][InlineData("2026-10-11T23:59:00+07:00","2026-10-05")]
    public void Weekly_schedule_is_monday_eight_vietnam(string now,string expected)=>Assert.Equal(DateOnly.Parse(expected),WeeklyReminderSchedule.DuePeriod(DateTimeOffset.Parse(now)));
    [Fact] public async Task Reminder_is_durable_deduplicated_and_rechecks_completed_or_cancelled_documents()
    {
        await using var f=await V2EditingTests.Fixture.Create();var h=Add(f.Db);await f.Db.SaveChangesAsync();var dir=new Directory();var transport=new Transport();var svc=new WeeklyReminders(f.Db,Service(f.Db),dir,transport,Clock);
        var batch=await svc.PlanAsync(Department);Assert.Equal(batch.Id,(await svc.PlanAsync(Department)).Id);var plan=System.Text.Json.JsonSerializer.Deserialize<ReminderPlan>(batch.PayloadJson)!;Assert.Single(plan.Envelopes);Assert.Single(plan.Envelopes[0].Cc);Assert.Equal("inputter@example.test",plan.Envelopes[0].To);
        h.Document!.Status="Cancelled";h.Version++;await f.Db.SaveChangesAsync();
        using var restarted=new DocumentDbContext(f.Options);var again=new WeeklyReminders(restarted,Service(restarted),dir,transport,Clock);Assert.Equal("NoRecipients",await again.DispatchAsync(batch.Id));Assert.Equal(0,transport.Calls);Assert.Equal("NotClaimed",await again.DispatchAsync(batch.Id));
    }
    [Fact] public async Task Missing_smtp_does_not_claim_sent_and_lost_dispatch_is_not_retried()
    {await using var f=await V2EditingTests.Fixture.Create();Add(f.Db);await f.Db.SaveChangesAsync();var svc=new WeeklyReminders(f.Db,Service(f.Db),new Directory(),new UnavailableReminderTransport(),Clock);var batch=await svc.PlanAsync(Department);Assert.Equal("PendingConfiguration",await svc.DispatchAsync(batch.Id));await f.Db.Set<ReminderBatch>().Where(x=>x.Id==batch.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Dispatching").SetProperty(x=>x.LeaseUntilUnix,0L));Assert.Equal("NotClaimed",await svc.DispatchAsync(batch.Id));Assert.Equal("UnknownOutcome",(await f.Db.Set<ReminderBatch>().AsNoTracking().SingleAsync()).State);}
    private sealed class Directory:IReminderDirectory
    {public Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([Department]);public Task<ReminderDirectory> ResolveAsync(Guid dep,CancellationToken ct)=>Task.FromResult(new ReminderDirectory(dep,Scope(),[new(User,true,"inputter@example.test")],[new(User,true,"INPUTTER@example.test"),new(Guid.NewGuid(),true,"leader@example.test"),new(Guid.NewGuid(),true,"LEADER@example.test")]));}
    private sealed class Transport:IReminderTransport{public int Calls;public Task<string> EnqueueAsync(Guid key,ReminderPlan plan,CancellationToken ct){Calls++;return Task.FromResult("Queued");}}
    private sealed class Files:IPdfFilesClient
    {public Task<PdfReceipt> InspectAsync(Guid id,CancellationToken ct)=>throw new PdfProtocolException(503,"UNAVAILABLE");public Task<PdfReceipt> PrepareAsync(PdfPrepare r,CancellationToken ct)=>throw new NotSupportedException();public Task<PdfReceipt> ActivateAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task RetireAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();}
    private sealed class AvailableFiles(PdfReceipt receipt):IPdfFilesClient
    {public PdfReceipt Receipt=receipt;public Task<PdfReceipt> InspectAsync(Guid id,CancellationToken ct)=>Task.FromResult(Receipt);public Task<PdfReceipt> PrepareAsync(PdfPrepare r,CancellationToken ct)=>throw new NotSupportedException();public Task<PdfReceipt> ActivateAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task RetireAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();}
}
internal static class XmlTestExtension {public static string Value(this XDocument document)=>document.Root!.Value;}
