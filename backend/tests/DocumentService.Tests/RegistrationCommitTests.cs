using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class RegistrationCommitTests
{
    [Fact]
    public async Task V2_commit_receipt_loss_does_not_duplicate_header_request_or_outbox()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var fault=new LostCommitReceipt();
        var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection,sql=>sql.ExecutionStrategy(d=>new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db=new DocumentDbContext(options);await db.Database.EnsureCreatedAsync();fault.Enabled=true;
        var saved=await new V2RegistrationService(db,new Clock()).RegisterAsync(V2PersistenceTests.Draft(),V2PersistenceTests.Identity,"lost-commit");
        Assert.Equal(1,fault.Thrown);Assert.Equal(1,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(1,await db.Documents.CountAsync());Assert.Equal(1,await db.DocumentStatusHistory.CountAsync());
        Assert.Equal(1,await db.DocumentRegistrations.CountAsync());Assert.Equal(1,await db.RegistrationRequests.CountAsync());
        Assert.Equal(1,await db.DocumentOutboxEvents.CountAsync());Assert.NotNull(saved.Registration);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_retries_by_registration_ID_without_allocating_again()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var fault=new LostCommitReceipt();
        var options=new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection,sql=>sql.ExecutionStrategy(d=>new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db=new DocumentDbContext(options);await db.Database.EnsureCreatedAsync();fault.Enabled=true;
        var doc=new Document{DocType="INCOMING",Title="Commit ambiguity",CreatedByUserId=Guid.NewGuid()};
        doc.StatusHistories.Add(new(){DocumentId=doc.Id,NewStatus=doc.Status,ChangedByUserId=doc.CreatedByUserId});
        var id=doc.Id;
        var result=await new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(doc,"HL",null);
        Assert.Equal(1,fault.Thrown);Assert.Equal(id,result.Id);Assert.Equal("27-01-0001/HL",result.DocumentNumber);
        Assert.Equal(1,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(1,await db.Documents.CountAsync());Assert.Equal(1,await db.DocumentStatusHistory.CountAsync());
    }
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>DateTimeOffset.Parse("2027-01-01T02:00:00Z");}
    private sealed class RetryOnce(ExecutionStrategyDependencies d):ExecutionStrategy(d,1,TimeSpan.Zero)
    {protected override bool ShouldRetryOn(Exception e)=>e is TimeoutException;}
    private sealed class LostCommitReceipt:DbTransactionInterceptor
    {
        public bool Enabled;public int Thrown;
        public override Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData e,CancellationToken token=default)
        {
            if(Enabled){Enabled=false;++Thrown;throw new TimeoutException("Injected receipt loss after actual COMMIT");}
            return Task.CompletedTask;
        }
    }
}
