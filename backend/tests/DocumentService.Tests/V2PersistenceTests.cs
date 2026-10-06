using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2PersistenceTests
{
    internal static readonly RegistrationIdentity Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ADM", "Administration");
    internal static V2RegistrationDraft Draft(string kind="OUTGOING", string subject="Registered subject") =>
        new(kind,"HL",subject,Identity.OriginatorUserId,Identity.OwnerDepartmentId,"Confidential",new(2020,3,4));

    [Fact]
    public async Task Server_date_sequence_identity_and_past_issued_date_are_persisted_separately()
    {
        await using var f=await Fixture.Create();
        var saved=await new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity,"key-1");
        f.Db.ChangeTracker.Clear();var header=await f.Db.Set<DocumentRegistration>().SingleAsync();
        Assert.Equal(saved.Id,header.DocumentId);Assert.Equal(new DateOnly(2027,1,1),header.RegistrationDate);
        Assert.Equal(2027,header.RegistrationYear);Assert.Equal(1,header.SequenceNumber);Assert.Equal(new DateOnly(2020,3,4),header.IssuedDate);
        Assert.Equal(Identity.OriginatorUserId,header.OriginatorUserId);Assert.Equal(Identity.InputterUserId,header.InputterUserId);
        Assert.Equal(Identity.OwnerDepartmentId,header.OwnerDepartmentId);Assert.Equal("ADM",header.OwnerDepartmentCodeSnapshot);
        Assert.Equal("27-01-0001/HL/ADM",saved.DocumentNumber);Assert.Equal("InProgress",saved.Status);
        Assert.Equal("Confidential",header.Sensitivity);Assert.Equal(1,header.Version);
        Assert.Equal(saved.CreatedAt,header.RegisteredAt.UtcDateTime);
        Assert.Single(await f.Db.DocumentStatusHistory.ToListAsync());
        Assert.Single(await f.Db.Set<RegistrationRequest>().ToListAsync());
        var ev=Assert.Single(await f.Db.Set<DocumentOutboxEvent>().ToListAsync());
        Assert.Equal("Pending",ev.State);Assert.Equal(saved.Id,ev.DocumentId);Assert.DoesNotContain("Registered subject",ev.PayloadJson);
    }

    [Fact]
    public async Task Same_key_normalized_body_returns_original_after_year_or_owner_name_change()
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock("2026-12-31T16:59:00Z"));
        var first=await service.RegisterAsync(Draft(subject:"  Registered subject  "),Identity,"key-1");
        var replay=await new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity with{OwnerDepartmentName="Changed name",OwnerDepartmentCode="IT"},"key-1");
        Assert.Equal(first.Id,replay.Id);Assert.Equal(first.DocumentNumber,replay.DocumentNumber);Assert.Equal(first.CreatedAt,replay.CreatedAt);
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Single(await f.Db.Set<DocumentOutboxEvent>().ToListAsync());
    }

    [Fact]
    public async Task Same_key_different_body_conflicts_without_allocating()
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock());
        await service.RegisterAsync(Draft(),Identity,"key-1");
        var conflict=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.RegisterAsync(Draft(subject:"Other subject"),Identity,"key-1"));
        Assert.Equal(409,conflict.Status);Assert.Equal("IDEMPOTENCY_CONFLICT",conflict.Code);
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Single(await f.Db.Set<DocumentOutboxEvent>().ToListAsync());
    }

    [Theory]
    [InlineData("owner")][InlineData("originator")][InlineData("sensitivity")]
    [InlineData("issuedDate")][InlineData("company")][InlineData("remark")]
    public async Task Idempotency_hash_includes_all_registration_intent_fields(string field)
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock());
        var draft=Draft();await service.RegisterAsync(draft,Identity,"intent-fields");
        var changed=field switch
        {
            "owner"=>draft with{OwnerDepartmentId=Guid.NewGuid()},
            "originator"=>draft with{OriginatorUserId=Guid.NewGuid()},
            "sensitivity"=>draft with{Sensitivity="Normal"},
            "issuedDate"=>draft with{IssuedDate=new(2021,1,1)},
            "company"=>draft with{CompanyCode="HV"},
            _=>draft with{Remark="Different remark"}
        };
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.RegisterAsync(changed,Identity,"intent-fields"));
        Assert.Equal("IDEMPOTENCY_CONFLICT",e.Code);Assert.Single(await f.Db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Committed_replay_survives_company_deactivation_but_deleted_resource_key_cannot_be_reused()
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock());
        var doc=await service.RegisterAsync(Draft(),Identity,"original");
        var company=await f.Db.BusinessCatalogEntries.SingleAsync(x=>x.Group=="companies"&&x.Code=="HL");
        company.IsActive=false;await f.Db.SaveChangesAsync();
        Assert.Equal(doc.Id,(await service.RegisterAsync(Draft(),Identity,"original")).Id);
        doc.IsDeleted=true;await f.Db.SaveChangesAsync();
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.RegisterAsync(Draft(),Identity,"original"));
        Assert.Equal("REGISTRATION_UNAVAILABLE",e.Code);Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Replay_checks_current_database_state_instead_of_a_previously_tracked_document()
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock());
        var first=await service.RegisterAsync(Draft(),Identity,"stale-context");
        await using(var other=new DocumentDbContext(f.Options))
        {
            var deleted=await other.Documents.SingleAsync();deleted.IsDeleted=true;await other.SaveChangesAsync();
        }
        Assert.False(first.IsDeleted);
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.RegisterAsync(Draft(),Identity,"stale-context"));
        Assert.Equal("REGISTRATION_UNAVAILABLE",e.Code);
    }

    [Fact]
    public async Task Header_preserves_four_digits_until_the_rare_transition_to_10000()
    {
        await using var f=await Fixture.Create();f.Db.DocumentNumberCounters.Add(new(){DocType="OUTGOING",Year=2027,CurrentValue=9998});await f.Db.SaveChangesAsync();
        var service=new V2RegistrationService(f.Db,new Clock());
        var lastFour=await service.RegisterAsync(Draft(),Identity,"last-four");
        var firstFive=await service.RegisterAsync(Draft(),Identity,"first-five");
        Assert.Equal("27-01-9999/HL/ADM",lastFour.DocumentNumber);Assert.Equal(9999,lastFour.Registration!.SequenceNumber);
        Assert.Equal("27-01-10000/HL/ADM",firstFive.DocumentNumber);Assert.Equal(10000,firstFive.Registration!.SequenceNumber);
    }

    [Fact]
    public async Task SQLite_G3_additions_preserve_legacy_data_and_existing_catalog_edits()
    {
        await using var f=await Fixture.Create();
        var old=new Document{DocType="INCOMING",Title="Historical",Status="Reviewed",DocumentNumber="20-12-0130/HL/HSE",CreatedByUserId=Guid.NewGuid()};
        f.Db.Documents.Add(old);f.Db.DocumentNumberCounters.Add(new(){DocType="INCOMING",Year=2020,CurrentValue=130});
        var company=await f.Db.BusinessCatalogEntries.FirstAsync();company.Name="Local name";await f.Db.SaveChangesAsync();
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE DocumentOutboxEvents; DROP TABLE RegistrationRequests; DROP TABLE DocumentRegistrations;");
        await SqliteG1Upgrade.ApplyAsync(f.Db);await SqliteG1Upgrade.ApplyAsync(f.Db);f.Db.ChangeTracker.Clear();
        var saved=await f.Db.Documents.SingleAsync();Assert.Equal(old.DocumentNumber,saved.DocumentNumber);Assert.Equal("Reviewed",saved.Status);
        Assert.Equal(130,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal("Local name",(await f.Db.BusinessCatalogEntries.SingleAsync(x=>x.Id==company.Id)).Name);
        Assert.Empty(await f.Db.DocumentRegistrations.ToListAsync());Assert.Empty(await f.Db.RegistrationRequests.ToListAsync());
        Assert.Empty(await f.Db.DocumentOutboxEvents.ToListAsync());
    }

    [Fact]
    public async Task Header_version_is_a_compare_and_set_token()
    {
        await using var f=await Fixture.Create();var saved=await new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity,"version");
        await using var other=new DocumentDbContext(f.Options);
        var stale=await other.DocumentRegistrations.SingleAsync();
        saved.Registration!.Version=2;saved.Registration.Remark="First edit";await f.Db.SaveChangesAsync();
        stale.Version=2;stale.Remark="Stale edit";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>other.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();Assert.Equal("First edit",(await f.Db.DocumentRegistrations.SingleAsync()).Remark);
    }

    [Theory]
    [InlineData("sequence")][InlineData("inputter")][InlineData("registrationDate")]
    public async Task Generated_sequence_inputter_and_registration_date_are_immutable(string field)
    {
        await using var f=await Fixture.Create();var saved=await new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity,"immutable");
        var header=saved.Registration!;
        if(field=="sequence")header.SequenceNumber=2;
        else if(field=="inputter")header.InputterUserId=Guid.NewGuid();
        else header.RegistrationDate=new(2027,2,1);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();var current=await f.Db.DocumentRegistrations.SingleAsync();
        Assert.Equal(Identity.OwnerDepartmentId,current.OwnerDepartmentId);Assert.Equal("HL",current.CompanyCode);
        Assert.Equal(new DateOnly(2027,1,1),current.RegistrationDate);
    }

    [Fact]
    public async Task SQLite_upgrade_failure_rolls_back_all_G3_additions_and_can_retry()
    {
        var fault=new SchemaFailure();await using var f=await Fixture.Create(fault);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE DocumentOutboxEvents; DROP TABLE RegistrationRequests; DROP TABLE DocumentRegistrations;");
        fault.Enabled=true;
        await Assert.ThrowsAsync<IOException>(()=>SqliteG1Upgrade.ApplyAsync(f.Db));
        await using var command=f.Db.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name IN ('DocumentOutboxEvents','RegistrationRequests','DocumentRegistrations')";
        Assert.Equal(0L,await command.ExecuteScalarAsync());
        Assert.Equal(21,await f.Db.BusinessCatalogEntries.CountAsync());
        fault.Enabled=false;await SqliteG1Upgrade.ApplyAsync(f.Db);
        Assert.Empty(await f.Db.DocumentRegistrations.ToListAsync());
    }

    [Fact]
    public async Task Key_is_case_sensitive_and_scoped_by_actor_and_kind()
    {
        await using var f=await Fixture.Create();var service=new V2RegistrationService(f.Db,new Clock());
        await service.RegisterAsync(Draft(),Identity,"Case-Key");await service.RegisterAsync(Draft(),Identity,"case-key");
        await service.RegisterAsync(Draft(),Identity with{InputterUserId=Guid.NewGuid()},"Case-Key");
        await service.RegisterAsync(Draft("INTERNAL"),Identity,"Case-Key");
        Assert.Equal(4,await f.Db.Set<RegistrationRequest>().CountAsync());Assert.Equal(4,await f.Db.Documents.CountAsync());
        Assert.Equal(3,(await f.Db.DocumentNumberCounters.SingleAsync(x=>x.DocType=="OUTGOING")).CurrentValue);
    }

    [Fact]
    public async Task Database_failure_rolls_back_number_header_receipt_audit_and_outbox()
    {
        var fault=new OutboxFailure();await using var f=await Fixture.Create(fault);
        var service=new V2RegistrationService(f.Db,new Clock());
        await Assert.ThrowsAsync<IOException>(()=>service.RegisterAsync(Draft(),Identity,"key-1"));
        Assert.False(f.Db.ChangeTracker.HasChanges());Assert.Empty(await f.Db.Documents.ToListAsync());
        Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());Assert.Empty(await f.Db.Set<DocumentRegistration>().ToListAsync());
        Assert.Empty(await f.Db.Set<RegistrationRequest>().ToListAsync());Assert.Empty(await f.Db.DocumentStatusHistory.ToListAsync());
        Assert.Empty(await f.Db.Set<DocumentOutboxEvent>().ToListAsync());
        fault.Enabled=false;Assert.EndsWith("0001/HL/ADM",(await service.RegisterAsync(Draft(),Identity,"key-1")).DocumentNumber);
    }

    [Theory]
    [InlineData("")][InlineData("space key")][InlineData("new\nline")]
    public async Task Invalid_key_fails_before_writing(string key)
    {
        await using var f=await Fixture.Create();
        var e=await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity,key));
        Assert.Equal(400,e.Status);Assert.Empty(await f.Db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Legacy_list_detail_and_mutations_cannot_access_v2_registration_even_as_admin()
    {
        await using var f=await Fixture.Create();var doc=await new V2RegistrationService(f.Db,new Clock()).RegisterAsync(Draft(),Identity,"key-1");
        var admin=new DocumentActor(Identity.InputterUserId,Identity.OwnerDepartmentId,new HashSet<string>{"Admin"});
        var legacy=new DocumentBusinessService(f.Db,new Notifications(),new Partners(),new Files());
        Assert.Empty((await legacy.GetListAsync(new(null,null,null,null,null,null,null),admin)).Items);
        Assert.Null(await legacy.GetByIdAsync(doc.Id,admin));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>legacy.ChangeStatusAsync(doc.Id,new("Distributed",null),admin));
        Assert.False(await legacy.DeleteAsync(doc.Id,admin));
        f.Db.DocumentAttachments.Add(new(){DocumentId=doc.Id,FileId=Guid.NewGuid()});await f.Db.SaveChangesAsync();
        Assert.False(await legacy.CanReadFileAsync((await f.Db.DocumentAttachments.SingleAsync()).FileId,admin));
        doc.SourceMessageId="v2-private-source";await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>legacy.CreateIncomingAsync(new("Fax",null,null,null,null,"v2-private-source"),admin));
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    private sealed class OutboxFailure:SaveChangesInterceptor
    {
        public bool Enabled=true;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Enabled&&e.Context!.ChangeTracker.Entries<DocumentOutboxEvent>().Any(x=>x.State==EntityState.Added))throw new IOException("Injected outbox persistence failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class SchemaFailure:DbCommandInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Enabled&&command.CommandText.StartsWith("CREATE TABLE \"RegistrationRequests\"",StringComparison.Ordinal))throw new IOException("Injected G3 schema creation failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Notifications:INotificationServiceClient{public Task<bool> SendNotificationAsync(SendNotificationRequest r)=>Task.FromResult(true);}
    private sealed class Partners:IPartnerServiceClient{public Task<PartnerDto?> GetPartnerByIdAsync(Guid id)=>Task.FromResult<PartnerDto?>(null);}
    private sealed class Files:IFilesServiceClient{public Task<FileMetadataDto?> GetFileByIdAsync(Guid id)=>Task.FromResult<FileMetadataDto?>(null);}
    internal sealed class Clock(string value="2026-12-31T17:00:00Z"):TimeProvider{public override DateTimeOffset GetUtcNow()=>DateTimeOffset.Parse(value);}
    private sealed class Fixture:IAsyncDisposable
    {
        private readonly SqliteConnection connection=new("Data Source=:memory:");public DocumentDbContext Db=null!;
        public DbContextOptions<DocumentDbContext> Options=null!;
        public static async Task<Fixture> Create(IInterceptor? interceptor=null)
        {
            var f=new Fixture();await f.connection.OpenAsync();var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(f.connection);
            if(interceptor is not null)options.AddInterceptors(interceptor);f.Options=options.Options;f.Db=new(f.Options);await f.Db.Database.EnsureCreatedAsync();return f;
        }
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await connection.DisposeAsync();}
    }
}
