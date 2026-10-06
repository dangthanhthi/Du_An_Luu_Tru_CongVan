using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2LifecycleTests
{
    internal static V2StatusDraft Action(Document d, V2StatusAction action, string? reason = null) => new(d.Registration!.Version, action, reason);
    internal static Task<Document> Change(DocumentDbContext db, Document d, V2StatusAction action, string? reason = null, V2EditorActor? actor = null) =>
        new V2DocumentLifecycle(db, V2EditingTests.Clock).ChangeAsync(d.Id, Action(d, action, reason), actor ?? V2EditingTests.Actor());
    internal static Task<Document> Register(DocumentDbContext db, string kind = "INTERNAL", bool recipients = true) =>
        new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(
            V2PersistenceTests.Draft(kind) with { IssuedDate = null, Details = kind switch {
                "OUTGOING" => V2KindDetailsTests.Outgoing() with { RecipientPartnerIds = recipients ? [V2KindDetailsTests.PartnerA] : [] },
                "INCOMING" => new(new(2026,12,30), V2KindDetailsTests.PartnerA, MethodCode: "EMAIL",
                    DistributionTargetIds: recipients ? [BusinessCatalogSeed.Targets().First().Id] : []),
                _ => new() } }, V2PersistenceTests.Identity, "lifecycle", references: V2KindDetailsTests.References());

    [Theory]
    [InlineData("InProgress")][InlineData("Distributed")]
    public async Task Restore_returns_to_previous_state_and_preserves_number_registration_files_and_counter(string state)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var d = await Register(f.Db);
        var file = new DocumentAttachment { DocumentId = d.Id, FileId = Guid.NewGuid(), AttachmentType = "PDF" };
        f.Db.DocumentAttachments.Add(file); await f.Db.SaveChangesAsync();
        if (state == "Distributed") d = await Change(f.Db, d, V2StatusAction.Distribute);
        var number = d.DocumentNumber; var distributedAt = d.DistributedAt; var startVersion = d.Registration!.Version;
        d = await Change(f.Db, d, V2StatusAction.Cancel, "  Entered by mistake  ");
        Assert.Equal("Cancelled", d.Status); Assert.False(d.IsDeleted);
        Assert.False(ReminderEligibility.IsEligible(d.DocType,d.Status,d.Registration!.RegistrationDate,false,d.Registration.IssuedDate,V2EditingTests.Clock.GetUtcNow()));
        d = await Change(f.Db, d, V2StatusAction.Restore);
        Assert.Equal(state, d.Status); Assert.Equal(startVersion + 2, d.Registration!.Version);
        Assert.Equal(number, d.DocumentNumber); Assert.Equal(distributedAt, d.DistributedAt);
        Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(file.FileId, (await f.Db.DocumentAttachments.SingleAsync()).FileId);
        Assert.Single(await f.Db.RegistrationRequests.ToListAsync()); Assert.Null(d.Registration.IssuedDate);
        Assert.False(DocumentCompletionEvaluator.Evaluate(d.Status,false,d.Registration.IssuedDate).IsComplete);
        Assert.True(ReminderEligibility.IsEligible(d.DocType,d.Status,d.Registration.RegistrationDate,false,d.Registration.IssuedDate,V2EditingTests.Clock.GetUtcNow()));
        var histories = await f.Db.DocumentStatusHistory.OrderBy(x=>x.ChangedAt).ToListAsync();
        Assert.Contains(histories, x=>x.OldStatus==state && x.NewStatus=="Cancelled" && x.Note=="Entered by mistake");
        Assert.Contains(histories, x=>x.OldStatus=="Cancelled" && x.NewStatus==state);
        using var audit = JsonDocument.Parse((await f.Db.Set<DocumentEditAudit>().SingleAsync(x=>x.Version==startVersion+1)).ChangesJson);
        Assert.Equal(state, audit.RootElement.GetProperty("Status").GetProperty("before").GetString());
        Assert.Equal("Cancelled", audit.RootElement.GetProperty("Status").GetProperty("after").GetString());
        Assert.All(await f.Db.DocumentOutboxEvents.ToListAsync(), x=> { Assert.Equal("Pending",x.State); Assert.DoesNotContain("mistake",x.PayloadJson); });
    }

    [Theory]
    [InlineData("INCOMING")][InlineData("OUTGOING")][InlineData("INTERNAL")]
    public async Task Distribution_allows_missing_pdf_and_issued_date_with_kind_specific_declared_recipients(string kind)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db,kind);
        d=await Change(f.Db,d,V2StatusAction.Distribute); Assert.Equal("Distributed",d.Status);
        Assert.Equal(V2EditingTests.Clock.GetUtcNow().UtcDateTime,d.DistributedAt); Assert.Null(d.Registration!.IssuedDate);
        Assert.Empty(await f.Db.DocumentAttachments.ToListAsync()); Assert.Empty(await f.Db.DocumentDepartmentAccess.ToListAsync());
        var version=d.Registration.Version; d=await Change(f.Db,d,V2StatusAction.Distribute); Assert.Equal(version,d.Registration!.Version);
        Assert.Single(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Theory]
    [InlineData("INCOMING")][InlineData("OUTGOING")]
    public async Task Other_recipients_text_does_not_substitute_for_typed_distribution_recipients(string kind)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db,kind,false);
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Distribute));
        Assert.Equal(400,e.Status); Assert.Equal("InProgress",(await f.Db.Documents.SingleAsync()).Status);
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Theory]
    [InlineData("inputter")][InlineData("originator")][InlineData("lineManager")][InlineData("deputyManager")]
    public async Task Active_edit_authority_can_cancel_confidential_documents(string role)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db); var a=Actor(role);
        d=await Change(f.Db,d,V2StatusAction.Cancel,"Reason",a); Assert.Equal("Cancelled",d.Status);
        if(role is "inputter" or "originator") Assert.Equal("InProgress",(await Change(f.Db,d,V2StatusAction.Restore,actor:a)).Status);
        else Assert.Equal(403,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Restore,actor:a))).Status);
    }
    private static V2EditorActor Actor(string role) {
        var a=V2EditingTests.Actor(role=="originator"?V2PersistenceTests.Identity.OriginatorUserId:role=="inputter"?null:Guid.NewGuid());
        return role switch { "lineManager"=>a with{LineManagerDepartmentIds=new HashSet<Guid>{V2PersistenceTests.Identity.OwnerDepartmentId}},
            "deputyManager"=>a with{DeputyManagerDepartmentIds=new HashSet<Guid>{V2PersistenceTests.Identity.OwnerDepartmentId}}, _=>a }; }

    [Theory]
    [InlineData("inactive")][InlineData("adminOnly")][InlineData("wrongManager")][InlineData("missingActor")]
    public async Task Invalid_authority_cannot_change_state(string role)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db);
        var a=role switch { "inactive"=>V2EditingTests.Actor() with{IsActive=false},"missingActor"=>V2EditingTests.Actor(Guid.Empty),
            "wrongManager"=>V2EditingTests.Actor(Guid.NewGuid()) with{LineManagerDepartmentIds=new HashSet<Guid>{Guid.NewGuid()}},_=>V2EditingTests.Actor(Guid.NewGuid()) };
        Assert.Contains((await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Cancel,"Reason",a))).Status,new[]{401,403});
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Theory]
    [InlineData("emptyReason")][InlineData("longReason")][InlineData("version")][InlineData("action")]
    public async Task Invalid_intent_is_rejected_without_mutation(string invalid)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db);
        var draft=invalid switch { "emptyReason"=>new V2StatusDraft(1,V2StatusAction.Cancel," "), "longReason"=>new(1,V2StatusAction.Cancel,new string('x',4001)),
            "version"=>new(0,V2StatusAction.Cancel,"Reason"),_=>new(1,(V2StatusAction)99,"Reason") };
        Assert.Equal(400,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentLifecycle(f.Db,V2EditingTests.Clock).ChangeAsync(d.Id,draft,V2EditingTests.Actor()))).Status);
        Assert.Equal(1,(await f.Db.DocumentRegistrations.SingleAsync()).Version);
    }

    [Fact]
    public async Task Stale_version_and_illegal_transitions_do_not_overwrite_cancellation_reason()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db); d=await Change(f.Db,d,V2StatusAction.Cancel,"Original");
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentLifecycle(f.Db,V2EditingTests.Clock).ChangeAsync(d.Id,new(1,V2StatusAction.Restore),V2EditingTests.Actor()))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Cancel,"Replace"))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Distribute))).Status);
        d=await Change(f.Db,d,V2StatusAction.Restore);
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Restore))).Status);
        Assert.Equal(2,await f.Db.Set<DocumentEditAudit>().CountAsync());
    }

    [Fact]
    public async Task Restore_distributed_revalidates_recipients_and_cancelled_metadata_remains_editable()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db,"OUTGOING"); d=await Change(f.Db,d,V2StatusAction.Distribute);
        d=await Change(f.Db,d,V2StatusAction.Cancel,"Reason");
        d=await new V2DocumentEditor(f.Db,V2EditingTests.Clock).UpdateAsync(d.Id,V2KindDetailsTests.Header(d,V2KindDetailsTests.Outgoing() with{RecipientPartnerIds=[]}),V2EditingTests.Actor());
        Assert.Equal(400,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Restore))).Status);
        Assert.Equal("Cancelled",(await f.Db.Documents.SingleAsync()).Status);
        d=await new V2DocumentEditor(f.Db,V2EditingTests.Clock).UpdateAsync(d.Id,V2KindDetailsTests.Header(d,V2KindDetailsTests.Outgoing()),V2EditingTests.Actor(),references:V2KindDetailsTests.References());
        Assert.Equal("Distributed",(await Change(f.Db,d,V2StatusAction.Restore)).Status);
    }

    [Fact]
    public async Task Distributed_edit_cannot_remove_last_recipient_or_partially_change_header()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db,"OUTGOING"); d.Status="Distributed"; await f.Db.SaveChangesAsync();
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,V2EditingTests.Clock).UpdateAsync(d.Id,
            V2KindDetailsTests.Header(d,V2KindDetailsTests.Outgoing() with{RecipientPartnerIds=[]}) with{Subject="Must rollback"},V2EditingTests.Actor()));
        Assert.Equal(400,e.Status); Assert.Equal("Registered subject",(await f.Db.Documents.SingleAsync()).Title); Assert.Single(await f.Db.Set<DocumentRecipient>().ToListAsync());
    }

    [Theory]
    [InlineData("Cancelled")][InlineData("Reviewed")]
    public async Task Historical_unknown_state_or_cancelled_without_provenance_requires_reconciliation(string state)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db); d.Status=state; await f.Db.SaveChangesAsync();
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Restore))).Status);
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task Audit_failure_rolls_back_status_history_and_cancellation_record()
    {
        var fault=new V2EditingTests.AuditFailure(); await using var f=await V2EditingTests.Fixture.Create(fault); var d=await Register(f.Db);
        await Assert.ThrowsAsync<IOException>(()=>Change(f.Db,d,V2StatusAction.Cancel,"Reason"));
        Assert.Equal("InProgress",(await f.Db.Documents.SingleAsync()).Status); Assert.Single(await f.Db.DocumentStatusHistory.ToListAsync());
        Assert.Single(await f.Db.DocumentOutboxEvents.ToListAsync()); fault.Enabled=false;
        d=await Change(f.Db,d,V2StatusAction.Cancel,"Reason"); Assert.Equal("InProgress",(await Change(f.Db,d,V2StatusAction.Restore)).Status);
    }

    [Fact]
    public async Task Repeated_cancellation_cycles_keep_each_reason_in_audit_and_history()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db);
        d=await Change(f.Db,d,V2StatusAction.Cancel,"First reason"); d=await Change(f.Db,d,V2StatusAction.Restore);
        d=await Change(f.Db,d,V2StatusAction.Distribute); d=await Change(f.Db,d,V2StatusAction.Cancel,"Second reason");
        var c=await f.Db.Set<DocumentCancellation>().SingleAsync(); Assert.Equal("Distributed",c.PreviousStatus); Assert.Null(c.RestoredAt);
        d=await Change(f.Db,d,V2StatusAction.Restore); Assert.Equal("Distributed",d.Status);
        Assert.Equal(6,d.Registration!.Version); Assert.Equal(V2EditingTests.Actor().UserId,(await f.Db.Set<DocumentCancellation>().SingleAsync()).RestoredByUserId);
        Assert.Contains(await f.Db.DocumentStatusHistory.ToListAsync(),x=>x.Note=="First reason");
        Assert.Contains(await f.Db.DocumentStatusHistory.ToListAsync(),x=>x.Note=="Second reason");
        var audits=await f.Db.Set<DocumentEditAudit>().ToListAsync(); Assert.Equal(5,audits.Count);
        Assert.Contains(audits,x=>x.ChangesJson.Contains("First reason")); Assert.Contains(audits,x=>x.ChangesJson.Contains("Second reason"));
    }

    [Theory]
    [InlineData("deleted")][InlineData("legacy")][InlineData("inconsistent")][InlineData("versionLimit")]
    public async Task Unavailable_or_inconsistent_resources_cannot_transition(string type)
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db); var expected=409;
        if(type=="deleted") { d.IsDeleted=true;expected=404; }
        if(type=="legacy") { f.Db.DocumentRegistrations.Remove(d.Registration!); expected=404; }
        if(type=="inconsistent") d.SenderDepartmentId=Guid.NewGuid();
        if(type=="versionLimit") d.Registration!.Version=long.MaxValue;
        var intent = new V2StatusDraft(type=="versionLimit"?long.MaxValue:1,V2StatusAction.Cancel,"Reason");
        await f.Db.SaveChangesAsync();
        Assert.Equal(expected,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentLifecycle(f.Db,V2EditingTests.Clock).ChangeAsync(d.Id,intent,V2EditingTests.Actor()))).Status);
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task Lifecycle_reads_current_version_instead_of_stale_tracker()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db);
        await using(var other=new DocumentDbContext(f.Options)) { var h=await other.DocumentRegistrations.SingleAsync(); h.Version=2; await other.SaveChangesAsync(); }
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Cancel,"Reason"))).Status);
        Assert.Equal("InProgress",(await f.Db.Documents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Incoming_header_only_registration_cannot_distribute_even_with_a_typed_recipient()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await f.Register("INCOMING");
        f.Db.Set<DocumentRecipient>().Add(new(){DocumentId=d.Id,ReferenceType="DistributionTarget",ReferenceId=BusinessCatalogSeed.Targets().First().Id,NameSnapshot="Target"});
        await f.Db.SaveChangesAsync(); Assert.Equal(400,(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Distribute))).Status);
    }

    [Fact]
    public async Task Sqlite_upgrade_adds_cancellation_table_without_rewriting_existing_registration()
    {
        await using var f=await V2EditingTests.Fixture.Create(); var d=await Register(f.Db);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE DocumentCancellations");
        await SqliteG1Upgrade.ApplyAsync(f.Db); await SqliteG1Upgrade.ApplyAsync(f.Db);
        d=await Change(f.Db,d,V2StatusAction.Cancel,"After upgrade"); Assert.Equal("InProgress",(await Change(f.Db,d,V2StatusAction.Restore)).Status);
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Actual_save_then_failure_rolls_back_cancellation_history_audit_and_outbox()
    {
        var fault=new AfterSaveFailure(); await using var f=await V2EditingTests.Fixture.Create(fault); var d=await Register(f.Db); fault.Enabled=true;
        await Assert.ThrowsAsync<IOException>(()=>Change(f.Db,d,V2StatusAction.Cancel,"Rollback"));
        Assert.Empty(await f.Db.Set<DocumentCancellation>().ToListAsync()); Assert.Equal("InProgress",(await f.Db.Documents.SingleAsync()).Status);
        Assert.Equal(1,(await f.Db.DocumentRegistrations.SingleAsync()).Version); Assert.Single(await f.Db.DocumentStatusHistory.ToListAsync());
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync()); Assert.Single(await f.Db.DocumentOutboxEvents.ToListAsync());
    }
    private sealed class AfterSaveFailure:SaveChangesInterceptor {
        public bool Enabled;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e,int result,CancellationToken ct=default) {
            if(Enabled) { Enabled=false;throw new IOException("Failure after actual SaveChanges"); } return ValueTask.FromResult(result); } }

    [Theory]
    [InlineData("lineManager")][InlineData("deputyManager")]
    public async Task Scoped_managers_can_distribute_without_acquiring_restore_permission(string role)
    {
        await using var f=await V2EditingTests.Fixture.Create();var d=await Register(f.Db);
        Assert.Equal("Distributed",(await Change(f.Db,d,V2StatusAction.Distribute,actor:Actor(role))).Status);
    }

    [Theory]
    [InlineData("activeRecordInProgress")][InlineData("restoredRecordCancelled")][InlineData("inactiveRestorer")]
    public async Task Restore_rejects_inconsistent_record_or_inactive_participant(string invalid)
    {
        await using var f=await V2EditingTests.Fixture.Create();var d=await Register(f.Db);
        d=await Change(f.Db,d,V2StatusAction.Cancel,"Reason");var c=await f.Db.Set<DocumentCancellation>().SingleAsync();
        if(invalid=="activeRecordInProgress") d.Status="InProgress";
        if(invalid=="restoredRecordCancelled") {c.RestoredAt=V2EditingTests.Clock.GetUtcNow();c.RestoredByUserId=V2EditingTests.Actor().UserId;}
        await f.Db.SaveChangesAsync();var actor=invalid=="inactiveRestorer"?V2EditingTests.Actor() with{IsActive=false}:V2EditingTests.Actor();
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>Change(f.Db,d,V2StatusAction.Restore,actor:actor));
        Assert.Equal(invalid=="inactiveRestorer"?403:409,e.Status); Assert.Single(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_does_not_repeat_the_status_transition()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var fault=new LostCommit();
        var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection,s=>s.ExecutionStrategy(d=>new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db=new DocumentDbContext(options); await db.Database.EnsureCreatedAsync(); var d=await Register(db); fault.Enabled=true;
        d=await Change(db,d,V2StatusAction.Cancel,"Once"); Assert.Equal("Cancelled",d.Status); Assert.Equal(2,d.Registration!.Version);
        Assert.Single(await db.Set<DocumentCancellation>().ToListAsync()); Assert.Single(await db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(2,await db.DocumentStatusHistory.CountAsync()); Assert.Equal(2,await db.DocumentOutboxEvents.CountAsync()); Assert.Equal(1,fault.Thrown);
    }
    private sealed class RetryOnce(ExecutionStrategyDependencies d):ExecutionStrategy(d,1,TimeSpan.Zero) { protected override bool ShouldRetryOn(Exception e)=>e is TimeoutException; }
    private sealed class LostCommit:DbTransactionInterceptor {
        public bool Enabled; public int Thrown;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken ct=default) {
            if(Enabled) { Enabled=false;Thrown++;throw new TimeoutException("Lost COMMIT acknowledgement"); } return Task.CompletedTask; } }
}
