using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using Xunit;
namespace NotificationService.Tests;
public sealed class DeliveryPolicyTests
{
    [Fact] public async Task Preference_change_before_delivery_is_rechecked_and_cc_is_deduplicated()
    {await using var h=new NotificationHttpTests.Host();_ = h.CreateClient();using var scope=h.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var user=Guid.NewGuid();var record=await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(),"cc",new(user,"inputter@example.test","Subject","Body",null,"Info",null,["INPUTTER@example.test","leader@example.test","leader@example.test"]),default);var message=System.Text.Json.JsonSerializer.Deserialize<DurableMessage>(record.PayloadJson)!;Assert.Single(message.Cc!);Assert.Equal("leader@example.test",message.Cc![0]);db.Add(new UserNotificationPreference{UserId=user,EmailEnabled=false});await db.SaveChangesAsync();var sender=new Sender();Assert.Equal("NoEmail",await new DurableDelivery(db,sender,TimeProvider.System).ProcessAsync(record.Id,default));Assert.Equal(0,sender.Calls);}
    [Fact] public async Task Retryable_pre_send_errors_reach_a_bounded_dead_letter_state()
    {await using var h=new NotificationHttpTests.Host();_ = h.CreateClient();using var scope=h.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var record=await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(),"retry",new(null,"inputter@example.test","Subject","Body",null,"Info",null),default);var sender=new Sender{Result="Retryable"};var clock=new Clock();var delivery=new DurableDelivery(db,sender,clock);for(var i=0;i<4;i++){Assert.Equal("Retryable",await delivery.ProcessAsync(record.Id,default));clock.Now=clock.Now.AddHours(2);}Assert.Equal("DeadLetter",await delivery.ProcessAsync(record.Id,default));clock.Now=clock.Now.AddHours(2);Assert.Equal("NotClaimed",await delivery.ProcessAsync(record.Id,default));Assert.Equal(5,sender.Calls);}
    [Fact] public async Task Hub_negotiate_requires_an_authenticated_identity_even_with_a_user_id_query()
    {await using var h=new NotificationHttpTests.Host();using var c=h.CreateClient();Assert.Equal(System.Net.HttpStatusCode.Unauthorized,(await c.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1&userId="+Guid.NewGuid(),null)).StatusCode);}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Sender:IDeliveryEmail{public int Calls;public string Result="Sent";public Task<string> SendAsync(DurableMessage message,Guid id,CancellationToken ct){Calls++;return Task.FromResult(Result);}}
}
