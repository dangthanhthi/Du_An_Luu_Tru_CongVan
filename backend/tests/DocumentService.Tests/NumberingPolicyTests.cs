using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class NumberingPolicyTests
{
    [Theory]
    [InlineData("INCOMING",1,"HL",null,"27-01-0001/HL")]
    [InlineData("OUTGOING",99,"HV","C&P","27-01-0099/HV/C&P")]
    [InlineData("INTERNAL",999,"HLHV","ADM","27-01-0999/INT/HLHV/ADM")]
    [InlineData("OUTGOING",9999,"HL","MGT","27-01-9999/HL/MGT")]
    [InlineData("OUTGOING",10000,"HL","MGT","27-01-10000/HL/MGT")]
    public void V2_format_preserves_minimum_four_digits_and_distinct_kind_format(string kind,int sequence,string company,string? dept,string expected) =>
        Assert.Equal(expected,DocumentNumberFormatter.Format(kind,new(2027,1,1),sequence,company,dept));

    [Theory]
    [InlineData("Unknown","HL",null)]
    [InlineData("OUTGOING","BAD","ADM")]
    [InlineData("OUTGOING","HL",null)]
    [InlineData("OUTGOING","HL","ADM/IT")]
    [InlineData("INCOMING","HL","ADM")]
    public void Invalid_context_cannot_create_a_registration_number(string kind,string company,string? dept)=>
        Assert.Throws<ArgumentException>(()=>DocumentNumberFormatter.Format(kind,new(2027,1,1),1,company,dept));

    [Fact]
    public async Task V2_writer_shares_counter_across_company_and_department_and_rolls_back_unknown_six_digit_policy()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();var writer=new DocumentRegistrationWriter(db,new Clock());
        Assert.Equal("27-01-0001/HL/ADM",(await writer.RegisterV2Async(New("OUTGOING"),"HL","ADM")).DocumentNumber);
        Assert.Equal("27-01-0002/HV/C&P",(await writer.RegisterV2Async(New("OUTGOING"),"HV","C&P")).DocumentNumber);
        Assert.Equal("27-01-0001/INT/HL/MGT",(await writer.RegisterV2Async(New("INTERNAL"),"HL","MGT")).DocumentNumber);
        var counter=await db.DocumentNumberCounters.SingleAsync(x=>x.DocType=="OUTGOING");counter.CurrentValue=99999;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>writer.RegisterV2Async(New("OUTGOING"),"HL","ADM"));
        Assert.Equal(99999,(await db.DocumentNumberCounters.SingleAsync(x=>x.DocType=="OUTGOING")).CurrentValue);
        Assert.Equal(3,await db.Documents.CountAsync());
    }

    [Fact]
    public async Task Source_message_cannot_be_used_to_replay_a_different_kind()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();var writer=new DocumentRegistrationWriter(db,new Clock());
        var incoming=New("INCOMING");incoming.SourceMessageId="message-1";await writer.RegisterV2Async(incoming,"HL",null);
        var outgoing=New("OUTGOING");outgoing.SourceMessageId="message-1";
        await Assert.ThrowsAsync<ArgumentException>(()=>writer.RegisterV2Async(outgoing,"HL","ADM"));
        Assert.Single(await db.Documents.ToListAsync());Assert.Single(await db.DocumentNumberCounters.ToListAsync());
    }

    private static Document New(string kind)=>new(){DocType=kind,Title="Numbering fixture",CreatedByUserId=Guid.NewGuid()};

    [Fact]
    public async Task Every_registration_has_an_initial_audit_even_if_caller_supplies_no_history()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();var doc=New("INCOMING");
        var saved=await new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(doc,"HL",null);
        var audit=Assert.Single(await db.DocumentStatusHistory.ToListAsync());
        Assert.Equal(saved.Id,audit.DocumentId);Assert.Equal(saved.CreatedByUserId,audit.ChangedByUserId);
        Assert.Equal(saved.CreatedAt,audit.ChangedAt);Assert.Equal(saved.Status,audit.NewStatus);Assert.Null(audit.OldStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_initial_audit_is_rejected_before_allocating(bool duplicate)
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();var doc=New("INCOMING");
        doc.StatusHistories.Add(new(){DocumentId=doc.Id,NewStatus=doc.Status,ChangedByUserId=duplicate?doc.CreatedByUserId:Guid.NewGuid()});
        if(duplicate)doc.StatusHistories.Add(new(){DocumentId=doc.Id,NewStatus=doc.Status,ChangedByUserId=doc.CreatedByUserId});
        await Assert.ThrowsAsync<ArgumentException>(()=>new DocumentRegistrationWriter(db,new Clock()).RegisterV2Async(doc,"HL",null));
        Assert.Empty(await db.DocumentNumberCounters.ToListAsync());Assert.Empty(await db.Documents.ToListAsync());
        Assert.Empty(await db.DocumentStatusHistory.ToListAsync());
    }
    private sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>DateTimeOffset.Parse("2027-01-01T02:00:00Z");}
}
