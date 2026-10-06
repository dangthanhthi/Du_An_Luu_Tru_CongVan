using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2EditingTests
{
    internal static readonly Guid NewDepartment=Guid.NewGuid();
    internal static V2EditorActor Actor(Guid? id=null)=>new(id??V2PersistenceTests.Identity.InputterUserId,true,
        new HashSet<Guid>{V2PersistenceTests.Identity.OwnerDepartmentId,NewDepartment},new HashSet<Guid>(),new HashSet<Guid>());
    internal static V2EditDraft Draft(long version=1)=>new(version,"HV","Updated subject",
        V2PersistenceTests.Identity.OriginatorUserId,NewDepartment,"Normal",new(2025,6,7),"Updated remark");
    internal static V2EditTarget Target()=>new(V2PersistenceTests.Identity.OriginatorUserId,NewDepartment,"FIN","Finance",true,true);
    internal static readonly TimeProvider Clock=new V2PersistenceTests.Clock("2028-04-09T01:02:03Z");

    [Fact]
    public async Task Changing_company_and_department_reformats_text_but_preserves_registration_sequence_and_date()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();
        var result=await new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),Actor(),Target());
        Assert.Equal(doc.Id,result.Id);Assert.Equal("27-01-0001/HV/FIN",result.DocumentNumber);
        var h=result.Registration!;Assert.Equal(1,h.SequenceNumber);Assert.Equal(2027,h.RegistrationYear);
        Assert.Equal(new DateOnly(2027,1,1),h.RegistrationDate);Assert.Equal(doc.CreatedAt,h.RegisteredAt.UtcDateTime);
        Assert.Equal(2,h.Version);Assert.Equal(NewDepartment,h.OwnerDepartmentId);Assert.Equal("Finance",h.OwnerDepartmentNameSnapshot);
        Assert.Equal("HV",h.CompanyCode);Assert.Equal("Updated subject",result.Title);Assert.Equal("Normal",h.Sensitivity);
        Assert.Equal(Actor().UserId,h.LastModifierUserId);Assert.Equal(Clock.GetUtcNow().UtcDateTime,result.UpdatedAt);
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        var audit=Assert.Single(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(result.Id,audit.DocumentId);Assert.Equal(Actor().UserId,audit.ActorUserId);Assert.Equal(2,audit.Version);
        using var changes=JsonDocument.Parse(audit.ChangesJson);
        Assert.Equal("HL",changes.RootElement.GetProperty("CompanyCode").GetProperty("before").GetString());
        Assert.Equal("HV",changes.RootElement.GetProperty("CompanyCode").GetProperty("after").GetString());
        var ev=await f.Db.DocumentOutboxEvents.SingleAsync(x=>x.Type=="DocumentUpdated");
        Assert.Equal(2,ev.AggregateVersion);Assert.Equal("Pending",ev.State);Assert.DoesNotContain("Updated subject",ev.PayloadJson);
        Assert.Single(await f.Db.RegistrationRequests.ToListAsync());Assert.Single(await f.Db.DocumentStatusHistory.ToListAsync());
    }

    [Theory]
    [InlineData("INCOMING","27-01-0001/HV")]
    [InlineData("INTERNAL","27-01-0001/INT/HV/FIN")]
    public async Task Reformat_uses_original_kind_specific_structure(string kind,string expected)
    {
        await using var f=await Fixture.Create();var doc=await f.Register(kind);
        Assert.Equal(expected,(await new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),Actor(),Target())).DocumentNumber);
    }

    [Fact]
    public async Task Stale_version_rejects_all_fields_without_an_extra_audit_event_or_counter()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();
        var editor=new V2DocumentEditor(f.Db,Clock);await editor.UpdateAsync(doc.Id,Draft(),Actor(),Target());
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>editor.UpdateAsync(doc.Id,Draft() with{Subject="Stale overwrite"},Actor(),Target()));
        Assert.Equal(409,e.Status);f.Db.ChangeTracker.Clear();Assert.Equal("Updated subject",(await f.Db.Documents.SingleAsync()).Title);
        Assert.Single(await f.Db.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(2,await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Theory]
    [InlineData("inactive")][InlineData("adminOnly")][InlineData("wrongManager")][InlineData("missingActor")]
    public async Task Unauthorized_actor_cannot_edit_even_if_the_browser_knows_valid_destination_data(string caseName)
    {
        await using var f=await Fixture.Create();var doc=await f.Register();var actor=Actor(Guid.NewGuid());
        actor=caseName switch {
            "inactive"=>Actor() with{IsActive=false},
            "missingActor"=>Actor(Guid.Empty),
            "wrongManager"=>actor with{LineManagerDepartmentIds=new HashSet<Guid>{NewDepartment}},
            _=>actor // Admin has no implicit business authority in this actor contract.
        };
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),actor,Target()));
        Assert.Contains(e.Status,new[]{401,403});Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(1,await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Theory]
    [InlineData("originator")][InlineData("lineManager")][InlineData("deputyManager")]
    public async Task Originator_and_scoped_managers_can_edit_confidential_header(string role)
    {
        await using var f=await Fixture.Create();var doc=await f.Register();var owner=V2PersistenceTests.Identity.OwnerDepartmentId;
        var actor=Actor(role=="originator"?V2PersistenceTests.Identity.OriginatorUserId:Guid.NewGuid());
        if(role=="lineManager")actor=actor with{LineManagerDepartmentIds=new HashSet<Guid>{owner}};
        if(role=="deputyManager")actor=actor with{DeputyManagerDepartmentIds=new HashSet<Guid>{owner}};
        Assert.Equal("Updated subject",(await new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),actor,Target())).Title);
    }

    [Theory]
    [InlineData("missing")][InlineData("mismatch")][InlineData("inactiveOriginator")]
    [InlineData("inactiveDepartment")][InlineData("outsideActorMembership")][InlineData("badCode")]
    public async Task Owner_changes_require_matching_active_verified_references_and_destination_scope(string invalid)
    {
        await using var f=await Fixture.Create();var doc=await f.Register();var actor=Actor();V2EditTarget? target=Target();
        target=invalid switch {
            "missing"=>null,"mismatch"=>target with{OwnerDepartmentId=Guid.NewGuid()},
            "inactiveOriginator"=>target with{OriginatorIsActive=false},"inactiveDepartment"=>target with{DepartmentIsActive=false},
            "badCode"=>target with{OwnerDepartmentCode="bad/code"},_=>target
        };
        if(invalid=="outsideActorMembership")actor=actor with{DepartmentIds=new HashSet<Guid>{V2PersistenceTests.Identity.OwnerDepartmentId}};
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),actor,target));
        Assert.Contains(e.Status,new[]{400,403});Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal("27-01-0001/HL/ADM",(await f.Db.Documents.SingleAsync()).DocumentNumber);
    }

    [Fact]
    public async Task Unchanged_scope_keeps_historical_names_without_resolving_current_originator_membership()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();var owner=V2PersistenceTests.Identity.OwnerDepartmentId;
        var company=await f.Db.BusinessCatalogEntries.SingleAsync(x=>x.Group=="companies"&&x.Code=="HL");
        company.Name="Renamed current catalog";company.IsActive=false;await f.Db.SaveChangesAsync();
        var draft=Draft() with{CompanyCode="HL",OwnerDepartmentId=owner};
        var result=await new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,draft,Actor() with{DepartmentIds=new HashSet<Guid>()});
        Assert.Equal(doc.Registration!.CompanyNameSnapshot,result.Registration!.CompanyNameSnapshot);
        Assert.NotEqual(company.Name,result.Registration.CompanyNameSnapshot);Assert.Equal("Administration",result.Registration.OwnerDepartmentNameSnapshot);
        Assert.Equal("27-01-0001/HL/ADM",result.DocumentNumber);
    }

    [Fact]
    public async Task Inactive_new_company_is_rejected_without_mutation()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();
        (await f.Db.BusinessCatalogEntries.SingleAsync(x=>x.Group=="companies"&&x.Code=="HV")).IsActive=false;await f.Db.SaveChangesAsync();
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),Actor(),Target()));
        Assert.Equal(400,e.Status);Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task No_op_does_not_advance_version_or_append_audit_and_preserves_normalization()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();var h=doc.Registration!;
        var draft=new V2EditDraft(1," hl "," Registered subject ",h.OriginatorUserId,h.OwnerDepartmentId,"Confidential",h.IssuedDate,"   ");
        var result=await new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,draft,Actor());
        Assert.Equal(1,result.Registration!.Version);Assert.Null(result.UpdatedAt);
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(1,await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Fact]
    public async Task Edit_reads_fresh_resource_instead_of_stale_EF_tracker()
    {
        await using var f=await Fixture.Create();var doc=await f.Register();
        await using(var other=new DocumentDbContext(f.Options)) {var h=await other.DocumentRegistrations.SingleAsync();h.Remark="Other context";h.Version=2;await other.SaveChangesAsync();}
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),Actor(),Target()));
        Assert.Equal(409,e.Status);Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Theory]
    [InlineData("deleted")][InlineData("legacy")]
    public async Task Deleted_and_legacy_resources_are_not_v2_edit_targets(string type)
    {
        await using var f=await Fixture.Create();Document doc;
        if(type=="legacy") {doc=new(){DocType="OUTGOING",Title="Legacy",DocumentNumber="CV-DI-2027-0001",CreatedByUserId=Actor().UserId};f.Db.Documents.Add(doc);await f.Db.SaveChangesAsync();}
        else {doc=await f.Register();doc.IsDeleted=true;await f.Db.SaveChangesAsync();}
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,Draft(),Actor(),Target()));
        Assert.Equal(404,e.Status);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_number_text_header_and_event_and_allows_retry()
    {
        var fault=new AuditFailure();await using var f=await Fixture.Create(fault);var doc=await f.Register();
        var editor=new V2DocumentEditor(f.Db,Clock);
        await Assert.ThrowsAsync<IOException>(()=>editor.UpdateAsync(doc.Id,Draft(),Actor(),Target()));
        Assert.False(f.Db.ChangeTracker.HasChanges());Assert.Equal("27-01-0001/HL/ADM",(await f.Db.Documents.SingleAsync()).DocumentNumber);
        Assert.Equal(1,(await f.Db.DocumentRegistrations.SingleAsync()).Version);Assert.Equal(1,await f.Db.DocumentOutboxEvents.CountAsync());
        fault.Enabled=false;Assert.Equal(2,(await editor.UpdateAsync(doc.Id,Draft(),Actor(),Target())).Registration!.Version);
    }

    [Fact]
    public async Task Lost_edit_commit_acknowledgement_returns_the_committed_version_without_duplicate_audit()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var fault=new LostEditCommit();
        var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection,s=>s.ExecutionStrategy(d=>new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db=new DocumentDbContext(options);await db.Database.EnsureCreatedAsync();
        var first=await new V2RegistrationService(db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"lost-edit");
        fault.Enabled=true;
        var edited=await new V2DocumentEditor(db,Clock).UpdateAsync(first.Id,Draft(),Actor(),Target());
        Assert.Equal(1,fault.Thrown);Assert.Equal(2,edited.Registration!.Version);Assert.Equal("27-01-0001/HV/FIN",edited.DocumentNumber);
        Assert.Single(await db.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(2,await db.DocumentOutboxEvents.CountAsync());
        Assert.Equal(1,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Theory]
    [InlineData("subject")][InlineData("remark")][InlineData("version")][InlineData("originator")][InlineData("sensitivity")]
    public async Task Invalid_edits_do_not_create_a_partial_graph(string field)
    {
        await using var f=await Fixture.Create();var doc=await f.Register();
        var draft=field switch {"subject"=>Draft() with{Subject=new string('x',2001)},"remark"=>Draft() with{Remark=new string('x',4001)},
            "version"=>Draft(0),"originator"=>Draft() with{OriginatorUserId=Guid.Empty},_=>Draft() with{Sensitivity="Other"}};
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2DocumentEditor(f.Db,Clock).UpdateAsync(doc.Id,draft,Actor(),Target()));
        Assert.Equal(400,e.Status);Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(1,(await f.Db.DocumentRegistrations.SingleAsync()).Version);Assert.Single(await f.Db.DocumentOutboxEvents.ToListAsync());
    }

    private sealed class RetryOnce(ExecutionStrategyDependencies d):ExecutionStrategy(d,1,TimeSpan.Zero)
    {protected override bool ShouldRetryOn(Exception e)=>e is TimeoutException;}
    private sealed class LostEditCommit:DbTransactionInterceptor
    {
        public bool Enabled;public int Thrown;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken ct=default)
        {if(Enabled){Enabled=false;Thrown++;throw new TimeoutException("Injected edit acknowledgement loss after COMMIT");}return Task.CompletedTask;}
    }

    internal sealed class AuditFailure:SaveChangesInterceptor
    {
        public bool Enabled=true;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> r,CancellationToken ct=default)
        {if(Enabled&&e.Context!.ChangeTracker.Entries<DocumentEditAudit>().Any(x=>x.State==EntityState.Added))throw new IOException("Injected audit failure");return ValueTask.FromResult(r);}
    }
    internal sealed class Fixture:IAsyncDisposable
    {
        private readonly SqliteConnection connection=new("Data Source=:memory:");
        public DocumentDbContext Db=null!;public DbContextOptions<DocumentDbContext> Options=null!;
        public static async Task<Fixture> Create(IInterceptor? interceptor=null)
        {var f=new Fixture();await f.connection.OpenAsync();var b=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(f.connection);if(interceptor is not null)b.AddInterceptors(interceptor);f.Options=b.Options;f.Db=new(f.Options);await f.Db.Database.EnsureCreatedAsync();return f;}
        public Task<Document> Register(string kind="OUTGOING")=>new V2RegistrationService(Db,new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(kind),V2PersistenceTests.Identity,"original");
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await connection.DisposeAsync();}
    }
}
