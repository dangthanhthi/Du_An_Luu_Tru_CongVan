using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using Xunit;
namespace NotificationService.Tests;
public sealed class DurableNotificationTests
{
    [Fact] public async Task Durable_acceptance_replay_and_subject_identity_isolation()
    {
        await using var host=new NotificationHttpTests.Host();var sender=Guid.NewGuid();var recipient=Guid.NewGuid();using var c=host.Client(sender,"NotificationSend");c.DefaultRequestHeaders.Add("Idempotency-Key","event-key");var message=new{recipientUserId=recipient,recipientEmail="recipient@example.test",subject="Fixture",body="Fixture body"};
        var first=await c.PostAsJsonAsync("/api/notifications/send",message);Assert.Equal(HttpStatusCode.Accepted,first.StatusCode);Assert.Equal(HttpStatusCode.Accepted,(await c.PostAsJsonAsync("/api/notifications/send",message)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/notifications/send",message with{subject="Other"})).StatusCode);
        using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();Assert.Single(await db.Set<DeliveryInbox>().ToListAsync());Assert.Single(await db.InAppNotifications.ToListAsync());
        using var owner=host.Client(recipient);using var stranger=host.Client(Guid.NewGuid());var id=(await db.InAppNotifications.SingleAsync()).Id;
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.PutAsync($"/api/notifications/{id}/read",null)).StatusCode);Assert.Equal(HttpStatusCode.OK,(await owner.PutAsync($"/api/notifications/{id}/read",null)).StatusCode);
        using var fresh=host.Services.CreateScope();var persisted=fresh.ServiceProvider.GetRequiredService<NotificationDbContext>();Assert.Equal("PendingConfiguration",await new DurableDelivery(persisted,new ConfiguredDeliveryEmail(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),TimeProvider.System).ProcessAsync((await persisted.Set<DeliveryInbox>().SingleAsync()).Id,default));
        Assert.Empty(await persisted.NotificationLogs.ToListAsync());
    }
    [Fact] public async Task Unknown_smtp_outcome_is_not_blindly_retried()
    {
        await using var host=new NotificationHttpTests.Host();_ = host.CreateClient();using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var inbox=await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(),"key",new(null,"recipient@example.test","Subject","Body",null,"Info",null),default);var fake=new Email("UnknownOutcome");
        var delivery=new DurableDelivery(db,fake,TimeProvider.System);Assert.Equal("UnknownOutcome",await delivery.ProcessAsync(inbox.Id,default));Assert.Equal("NotClaimed",await delivery.ProcessAsync(inbox.Id,default));Assert.Equal(1,fake.Calls);
    }
    [Fact] public async Task Expired_sending_lease_becomes_unknown_and_never_resends()
    {await using var host=new NotificationHttpTests.Host();_ = host.CreateClient();using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var inbox=await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(),"crash",new(null,"recipient@example.test","Subject","Body",null,"Info",null),default);await db.Set<DeliveryInbox>().Where(x=>x.Id==inbox.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Sending").SetProperty(x=>x.LeaseUntilUnix,0L));var fake=new Email("Sent");Assert.Equal("NotClaimed",await new DurableDelivery(db,fake,TimeProvider.System).ProcessAsync(inbox.Id,default));Assert.Equal(0,fake.Calls);Assert.Equal("UnknownOutcome",(await db.Set<DeliveryInbox>().AsNoTracking().SingleAsync()).State);}
    [Fact] public async Task Preference_suppression_and_invalid_redirect_are_enforced()
    {await using var host=new NotificationHttpTests.Host();_ = host.CreateClient();using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var user=Guid.NewGuid();db.Add(new UserNotificationPreference{UserId=user,EmailEnabled=false,InAppEnabled=false});await db.SaveChangesAsync();var svc=new DurableNotifications(db);var r=await svc.AcceptAsync(Guid.NewGuid(),"prefs",new(user,"recipient@example.test","Subject","Body",null,"Info",null),default);Assert.Equal("NoEmail",r.State);Assert.Empty(await db.InAppNotifications.ToListAsync());await Assert.ThrowsAsync<NotificationRuleException>(()=>svc.AcceptAsync(Guid.NewGuid(),"bad",new(user,null,"Subject","Body",null,"Info","//foreign.test"),default));}
    private sealed class Email(string result):IDeliveryEmail{public int Calls;public Task<string> SendAsync(DurableMessage m,Guid id,CancellationToken ct){Calls++;return Task.FromResult(result);}}
}
