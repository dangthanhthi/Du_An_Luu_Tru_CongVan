using Xunit;

namespace DocumentService.Tests;

public sealed class ReminderEligibilityTests
{
    [Theory]
    [InlineData("Distributed",true,true,true,"")]
    [InlineData("Distributed",false,true,false,"MissingPdf")]
    [InlineData("Distributed",true,false,false,"MissingIssuedDate")]
    [InlineData("Distributed",false,false,false,"MissingPdf,MissingIssuedDate")]
    [InlineData("InProgress",true,true,false,"NotDistributed")]
    [InlineData("InProgress",false,true,false,"NotDistributed,MissingPdf")]
    [InlineData("InProgress",true,false,false,"NotDistributed,MissingIssuedDate")]
    [InlineData("InProgress",false,false,false,"NotDistributed,MissingPdf,MissingIssuedDate")]
    public void Completeness_requires_distributed_available_current_PDF_and_issued_date(string status,bool pdf,bool date,bool complete,string issues)
    {
        var result=DocumentCompletionEvaluator.Evaluate(status,pdf,date?new(2020,1,1):null);
        Assert.Equal(complete,result.IsComplete);Assert.Equal(issues,string.Join(',',result.Issues));
    }

    [Theory]
    [InlineData("OUTGOING","InProgress",false,false,true)]
    [InlineData("INTERNAL","InProgress",true,true,true)]
    [InlineData("OUTGOING","Distributed",false,true,true)]
    [InlineData("INTERNAL","Distributed",true,false,true)]
    [InlineData("INCOMING","InProgress",false,false,false)]
    [InlineData("OUTGOING","Cancelled",false,false,false)]
    [InlineData("INTERNAL","Cancelled",true,true,false)]
    [InlineData("OUTGOING","Distributed",true,true,false)]
    public void Fourteen_day_reminders_only_include_incomplete_outgoing_and_internal_excluding_cancelled(string kind,string status,bool pdf,bool date,bool expected)
    {
        Assert.Equal(expected,ReminderEligibility.IsEligible(kind,status,new(2027,1,1),pdf,date?new(2020,1,1):null,DateTimeOffset.Parse("2027-01-16T02:00:00Z")));
    }

    [Theory]
    [InlineData("2027-01-14T16:59:59Z",false)] // VN Jan14:13 calendar days.
    [InlineData("2027-01-14T17:00:00Z",false)] // VN Jan15:exactly14 days.
    [InlineData("2027-01-15T16:59:59Z",false)]
    [InlineData("2027-01-15T17:00:00Z",true)] // VN Jan16:older than14 calendar days.
    [InlineData("2026-12-31T16:00:00Z",false)]
    public void Boundary_is_more_than_fourteen_calendar_days_in_Vietnam_not_UTC_or_working_days(string utc,bool expected)
    {
        Assert.Equal(expected,ReminderEligibility.IsEligible("OUTGOING","InProgress",new(2027,1,1),false,null,DateTimeOffset.Parse(utc)));
    }
}
