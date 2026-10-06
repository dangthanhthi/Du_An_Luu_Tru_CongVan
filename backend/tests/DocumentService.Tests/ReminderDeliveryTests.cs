using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReminderDeliveryTests
{
    internal static IConfiguration Config()=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Reminders:TransportEnabled","true"},{"Notifications:TransportEnabled","true"},{"Notifications:Endpoint","http://localhost/api/notifications/send"},{"Notifications:ServiceToken","Test-only-token"}}).Build();
    [Fact] public async Task Partial_ack_loss_replays_exact_key_body_and_keeps_accepted_recipient_out_of_retry()
    {
        await using var f=await V2EditingTests.Fixture.Create();var plan=ReminderFanoutTests.Plan();var other=plan.Envelopes[0] with{InputterUserId=Guid.NewGuid(),To="other@example.test",Documents=[plan.Envelopes[0].Documents[0] with{DocumentId=Guid.NewGuid()}]};plan=plan with{Envelopes=[plan.Envelopes[0],other]};var batch=await Batch(f.Db,plan);var clock=new Clock();var remote=new Receiver{LoseOnce=true};var service=new DurableReminderTransport(f.Db,remote,clock,Config());
        Assert.Equal("PendingAcceptance",await service.EnqueueAsync(batch.Id,plan,default));Assert.Equal(1,await f.Db.Set<ReminderDelivery>().CountAsync(x=>x.State=="Accepted"));Assert.Equal(2,remote.Inbox.Count);
        clock.Now=clock.Now.AddMinutes(5);Assert.Equal("Queued",await service.EnqueueAsync(batch.Id,plan with{EvaluatedAt=clock.Now},default));var rows=await f.Db.Set<ReminderDelivery>().AsNoTracking().ToArrayAsync();Assert.All(rows,x=>Assert.Equal("Accepted",x.State));Assert.Equal(2,rows.Select(x=>x.NotificationId).Distinct().Count());Assert.Equal(2,remote.Inbox.Count);Assert.Equal(3,remote.Calls);
    }
    [Fact] public async Task Changed_document_version_or_audience_stops_unconfirmed_delivery()
    {
        await using var f=await V2EditingTests.Fixture.Create();var plan=ReminderFanoutTests.Plan();var batch=await Batch(f.Db,plan);var remote=new Receiver{Reply="PendingConfiguration"};var service=new DurableReminderTransport(f.Db,remote,new Clock(),Config());Assert.Equal("PendingAcceptance",await service.EnqueueAsync(batch.Id,plan,default));
        var changed=plan with{Envelopes=[plan.Envelopes[0] with{Documents=[plan.Envelopes[0].Documents[0] with{Version=2}]}]};Assert.Equal("RequiresReconciliation",await service.EnqueueAsync(batch.Id,changed,default));Assert.Equal(1,remote.Calls);Assert.Empty(remote.Inbox);Assert.Single(await f.Db.Set<ReminderDelivery>().ToArrayAsync());
    }
    [Fact] public async Task Configuration_wait_does_not_consume_the_failure_budget_but_five_failures_stop_retry()
    {
        await using var f=await V2EditingTests.Fixture.Create();var plan=ReminderFanoutTests.Plan();var batch=await Batch(f.Db,plan);var remote=new Receiver{Reply="PendingConfiguration"};var clock=new Clock();var service=new DurableReminderTransport(f.Db,remote,clock,Config());
        for(var i=0;i<6;i++){Assert.Equal("PendingAcceptance",await service.EnqueueAsync(batch.Id,plan,default));clock.Now=clock.Now.AddMinutes(5);}Assert.Equal(0,(await f.Db.Set<ReminderDelivery>().AsNoTracking().SingleAsync()).Failures);
        remote.Reply="Retryable";for(var i=0;i<5;i++){Assert.Equal(i==4?"DeadLetter":"PendingAcceptance",await service.EnqueueAsync(batch.Id,plan,default));clock.Now=clock.Now.AddMinutes(5);}Assert.Equal("DeadLetter",await service.EnqueueAsync(batch.Id,plan,default));Assert.Equal(11,remote.Calls);Assert.Equal(5,(await f.Db.Set<ReminderDelivery>().AsNoTracking().SingleAsync()).Failures);
    }
    [Fact] public async Task Invalid_cc_or_duplicate_inputter_cannot_create_partial_manifest_or_send()
    {
        await using var f=await V2EditingTests.Fixture.Create();var plan=ReminderFanoutTests.Plan();var batch=await Batch(f.Db,plan);var remote=new Receiver();var service=new DurableReminderTransport(f.Db,remote,new Clock(),Config());
        foreach(var bad in new[]{plan with{Envelopes=[plan.Envelopes[0] with{Cc=Enumerable.Range(0,21).Select(i=>$"leader{i}@example.test").ToArray()}]},plan with{Envelopes=[plan.Envelopes[0],plan.Envelopes[0]]}})Assert.Equal("REMINDER_PLAN_INVALID",(await Assert.ThrowsAsync<DocumentRegistrationRuleException>(()=>service.EnqueueAsync(batch.Id,bad,default))).Code);
        Assert.Empty(await f.Db.Set<ReminderFanoutManifest>().ToListAsync());Assert.Empty(await f.Db.Set<ReminderDelivery>().ToListAsync());Assert.Equal(0,remote.Calls);
    }
    [Theory][InlineData("malformed")][InlineData("empty")][InlineData("wrong-state")][InlineData("large")][InlineData("redirect")]
    public async Task Bad_or_redirected_receipts_require_reconciliation(string mode)
    {using var http=new HttpClient(new Handler(mode));var result=await new ConfiguredReminderNotificationTransport(http,Config()).AcceptAsync(Guid.NewGuid(),new(Guid.NewGuid(),"to@example.test","Subject","Body",null,"Warning",null,[]),default);Assert.Equal("RequiresReconciliation",result.State);Assert.Null(result.ReceiptId);}
    [Fact] public async Task NoEmail_receipt_is_durable_acceptance_without_claiming_SMTP_sent()
    {using var http=new HttpClient(new Handler("no-email"));var result=await new ConfiguredReminderNotificationTransport(http,Config()).AcceptAsync(Guid.NewGuid(),new(Guid.NewGuid(),"to@example.test","Subject","Body",null,"Warning",null,[]),default);Assert.Equal("Accepted",result.State);Assert.Equal("NoEmail",result.InboxState);Assert.NotNull(result.ReceiptId);}
    internal static async Task<ReminderBatch> Batch(DocumentDbContext db,ReminderPlan plan){var b=new ReminderBatch{DepartmentId=plan.Envelopes[0].Documents[0].DepartmentId,Period=new(2026,10,5),CreatedAt=plan.EvaluatedAt,State="Dispatching",PayloadJson=JsonSerializer.Serialize(plan)};db.Add(b);await db.SaveChangesAsync();return b;}
    internal sealed class Clock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-05T01:00:00Z");public override DateTimeOffset GetUtcNow()=>Now;}
    internal sealed class Receiver:IReminderNotificationTransport
    {public bool LoseOnce;public string Reply="Accepted";public int Calls;public Dictionary<Guid,(Guid Receipt,string Body)> Inbox=[];public Task<ReminderAcceptance> AcceptAsync(Guid id,ReminderNotificationMessage msg,CancellationToken ct){Calls++;if(Reply!="Accepted")return Task.FromResult(new ReminderAcceptance(Reply));var body=JsonSerializer.Serialize(msg);if(Inbox.TryGetValue(id,out var existing))Assert.Equal(existing.Body,body);else Inbox.Add(id,(Guid.NewGuid(),body));if(LoseOnce){LoseOnce=false;throw new IOException("Lost acknowledgement after acceptance");}return Task.FromResult(new ReminderAcceptance("Accepted",Inbox[id].Receipt,"Queued"));}}
    private sealed class Handler(string mode):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(mode=="redirect"?HttpStatusCode.TemporaryRedirect:HttpStatusCode.Accepted){Content=mode switch{"malformed"=>new StringContent("bad"),"large"=>new StringContent(new string('x',17000)),"empty"=>JsonContent.Create(new{success=true,data=new{id=Guid.Empty,state="Queued"}}),"wrong-state"=>JsonContent.Create(new{success=true,data=new{id=Guid.NewGuid(),state="FakeSuccess"}}),_=>JsonContent.Create(new{success=true,data=new{id=Guid.NewGuid(),state="NoEmail"}})}});}
}
