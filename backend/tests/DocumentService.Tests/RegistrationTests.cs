using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace DocumentService.Tests;

public sealed class RegistrationTests
{
    private static readonly DocumentActor Admin = new(Guid.NewGuid(), null, new HashSet<string> { "Admin" });

    [Fact]
    public async Task Document_counter_and_history_rollback_together_and_context_can_retry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var fault = new FailDocumentSave();
        var options = new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).AddInterceptors(fault).Options;
        await using var db = new DocumentDbContext(options); await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CreateIncomingAsync(new("Failed", null, null, null, null), Admin));
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.DocumentNumberCounters.ToListAsync());
        Assert.Empty(await db.Documents.ToListAsync());
        Assert.Empty(await db.DocumentStatusHistory.ToListAsync());
        fault.Enabled = false;
        var result = await Service(db).CreateIncomingAsync(new("Retry", null, null, null, null), Admin);
        Assert.EndsWith("-0001", result.DocumentNumber);
        Assert.Equal(1, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Single(await db.DocumentStatusHistory.ToListAsync());
    }

    [Theory]
    [InlineData("2030-12-31T16:59:59+00:00", 2030)]
    [InlineData("2030-12-31T17:00:00+00:00", 2031)]
    public async Task Registration_uses_one_server_instant_and_Vietnam_business_year(string instant, int year)
    {
        await using var f = await Fixture.Create();
        var clock = new Clock(DateTimeOffset.Parse(instant));
        var result = await Service(f.Db, clock).CreateIncomingAsync(new("Boundary", null, null, new DateTime(1999, 1, 1), null), Admin);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, result.CreatedAt);
        Assert.Equal(year, (await f.Db.DocumentNumberCounters.SingleAsync()).Year);
        Assert.Equal($"CV-DEN-{year}-0001", result.DocumentNumber);
        Assert.Equal(new DateTime(1999,1,1), result.ReceivedAt);
        Assert.All(result.StatusHistories, h => Assert.Equal(result.CreatedAt, h.ChangedAt));
    }

    [Fact]
    public async Task Registered_owner_cannot_change_even_for_admin_and_rejection_does_not_mutate_metadata()
    {
        await using var f = await Fixture.Create();
        var owner = Guid.NewGuid();
        var registered = await Service(f.Db).CreateInternalAsync(new("Original", null, owner, null), Admin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f.Db).UpdateAsync(
            registered.Id, new("Changed", null, null, Guid.NewGuid(), null), Admin));
        Assert.Equal("Original", registered.Title);
        Assert.Equal(owner, registered.SenderDepartmentId);
        await f.Db.Entry(registered).ReloadAsync();
        Assert.Equal("Original", registered.Title);
        Assert.Equal(owner, registered.SenderDepartmentId);
    }

    [Fact]
    public async Task Same_owner_can_be_resubmitted_and_old_number_is_never_rewritten()
    {
        await using var f = await Fixture.Create(); var owner=Guid.NewGuid();
        var old=new Document { DocumentNumber="20-12-0130/HL/HSE", DocType="INTERNAL", Title="Historical", SenderDepartmentId=owner,CreatedByUserId=Admin.UserId };
        f.Db.Documents.Add(old); await f.Db.SaveChangesAsync();
        var result=await Service(f.Db).UpdateAsync(old.Id,new("Edited",null,null,owner,null),Admin);
        Assert.Equal("20-12-0130/HL/HSE",result.DocumentNumber);
        Assert.Equal(owner,result.SenderDepartmentId);
        Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());
    }

    [Fact]
    public async Task Three_kinds_have_independent_counters_and_four_digits_expand_only_at_10000()
    {
        await using var f=await Fixture.Create();
        var year=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).Year;
        f.Db.DocumentNumberCounters.Add(new() {DocType="INCOMING",Year=year,CurrentValue=9998}); await f.Db.SaveChangesAsync();
        var service=Service(f.Db);
        Assert.EndsWith("-9999",(await service.CreateIncomingAsync(new("Last four",null,null,null,null),Admin)).DocumentNumber);
        Assert.EndsWith("-10000",(await service.CreateIncomingAsync(new("First five",null,null,null,null),Admin)).DocumentNumber);
        Assert.EndsWith("-0001",(await service.CreateInternalAsync(new("Internal",null,Guid.NewGuid(),null),Admin)).DocumentNumber);
        Assert.EndsWith("-0001",(await service.CreateOutgoingAsync(new("Outgoing",null,Guid.NewGuid(),Guid.NewGuid(),null),Admin)).DocumentNumber);
        Assert.Equal(3,await f.Db.DocumentNumberCounters.CountAsync());
    }

    [Fact]
    public async Task Source_message_replay_returns_original_registration_and_does_not_allocate_again()
    {
        await using var f=await Fixture.Create();
        var firstClock=new Clock(DateTimeOffset.Parse("2026-12-31T16:00:00Z"));
        var first=await Service(f.Db,firstClock).CreateIncomingAsync(new("Fax",null,null,null,null,"mail-1"),Admin);
        var replay=await Service(f.Db,new Clock(DateTimeOffset.Parse("2027-01-01T01:00:00Z"))).CreateIncomingAsync(new("Fax",null,null,null,null,"mail-1"),Admin);
        Assert.Equal(first.Id,replay.Id);Assert.Equal(first.CreatedAt,replay.CreatedAt);
        Assert.Single(await f.Db.DocumentNumberCounters.ToListAsync());
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Overflow_never_wraps_counter_and_leaves_no_document()
    {
        await using var f=await Fixture.Create();
        var year=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).Year;
        f.Db.DocumentNumberCounters.Add(new() {DocType="INCOMING",Year=year,CurrentValue=int.MaxValue});await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<OverflowException>(()=>Service(f.Db).CreateIncomingAsync(new("Overflow",null,null,null,null),Admin));
        f.Db.ChangeTracker.Clear();Assert.Equal(int.MaxValue,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Empty(await f.Db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Source_replay_does_not_revalidate_external_partner_after_registration()
    {
        await using var f=await Fixture.Create();
        var request=new CreateIncomingDocumentRequest("Fax",null,Guid.NewGuid(),null,null,"partner-replay");
        var first=await Service(f.Db).CreateIncomingAsync(request,Admin);
        var replayService=new DocumentBusinessService(f.Db,new Notifications(),new MissingPartner(),new Files());
        var replay=await replayService.CreateIncomingAsync(request,Admin);
        Assert.Equal(first.Id,replay.Id);Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Deleted_source_is_still_processed_and_cannot_allocate_a_new_registration()
    {
        await using var f=await Fixture.Create();var service=Service(f.Db);
        var request=new CreateIncomingDocumentRequest("Fax",null,null,null,null,"deleted-source");
        var first=await service.CreateIncomingAsync(request,Admin);first.IsDeleted=true;await f.Db.SaveChangesAsync();
        Assert.True(await service.HasProcessedSourceMessageAsync("deleted-source"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CreateIncomingAsync(request,Admin));
        Assert.Equal(1,(await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Single(await f.Db.Documents.IgnoreQueryFilters().ToListAsync());
    }

    private static DocumentBusinessService Service(DocumentDbContext db,TimeProvider? clock=null)=>new(db,new Notifications(),new Partners(),new Files(),clock);
    private sealed class Clock(DateTimeOffset value):TimeProvider {public override DateTimeOffset GetUtcNow()=>value;}
    private sealed class Notifications:INotificationServiceClient {public Task<bool> SendNotificationAsync(SendNotificationRequest req)=>Task.FromResult(true);}
    private sealed class Partners:IPartnerServiceClient {public Task<PartnerDto?> GetPartnerByIdAsync(Guid id)=>Task.FromResult<PartnerDto?>(new(id,"Partner","P","Both",null,null,true));}
    private sealed class MissingPartner:IPartnerServiceClient {public Task<PartnerDto?> GetPartnerByIdAsync(Guid id)=>Task.FromResult<PartnerDto?>(null);}
    private sealed class Files:IFilesServiceClient {public Task<FileMetadataDto?> GetFileByIdAsync(Guid id)=>Task.FromResult<FileMetadataDto?>(new(id,"test.pdf",10,"application/pdf"));}
    private sealed class FailDocumentSave:SaveChangesInterceptor
    {
        public bool Enabled=true;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken token=default)
        {
            if(Enabled && e.Context!.ChangeTracker.Entries<Document>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Injected document persistence failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Fixture:IAsyncDisposable
    {
        private readonly SqliteConnection connection=new("Data Source=:memory:");
        public DocumentDbContext Db=null!;
        public static async Task<Fixture> Create(){var f=new Fixture();await f.connection.OpenAsync();f.Db=new(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(f.connection).Options);await f.Db.Database.EnsureCreatedAsync();return f;}
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await connection.DisposeAsync();}
    }
}
