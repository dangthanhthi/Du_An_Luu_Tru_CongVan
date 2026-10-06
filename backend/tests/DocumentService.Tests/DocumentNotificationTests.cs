using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class DocumentNotificationTests
{
    [Fact] public async Task Outbox_acceptance_is_durable_deduplicated_and_current_audience_is_rechecked()
    {await using var f=await V2EditingTests.Fixture.Create();await f.Register("INTERNAL");var change=await f.Db.DocumentOutboxEvents.SingleAsync();var audience=new Audience();var transport=new Transport();var svc=new DocumentNotifications(f.Db,audience,transport,TimeProvider.System);await svc.PlanAsync(change.Id,default);await svc.PlanAsync(change.Id,default);var delivery=await f.Db.Set<DocumentNotificationDelivery>().SingleAsync();Assert.Equal("Accepted",await svc.DispatchAsync(delivery.Id,default));Assert.Equal("NotClaimed",await svc.DispatchAsync(delivery.Id,default));Assert.Equal(1,transport.Calls);Assert.Equal("Pending",(await f.Db.DocumentOutboxEvents.SingleAsync()).State);}
    [Fact] public async Task Revoked_recipient_does_not_receive_the_notification()
    {await using var f=await V2EditingTests.Fixture.Create();await f.Register("INTERNAL");var change=await f.Db.DocumentOutboxEvents.SingleAsync();var audience=new Audience();var transport=new Transport();var svc=new DocumentNotifications(f.Db,audience,transport,TimeProvider.System);await svc.PlanAsync(change.Id,default);audience.Revoked=true;Assert.Equal("Suppressed",await svc.DispatchAsync((await f.Db.Set<DocumentNotificationDelivery>().SingleAsync()).Id,default));Assert.Equal(0,transport.Calls);}
    private sealed class Audience:IDocumentNotificationAudience
    {private readonly Guid user=Guid.NewGuid();public bool Revoked;public Task<IReadOnlySet<Guid>> RecipientsAsync(DocumentOutboxEvent e,CancellationToken ct)=>Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>{user});public Task<DocumentNotificationMessage?> ResolveAsync(DocumentOutboxEvent e,Guid recipient,CancellationToken ct)=>Task.FromResult<DocumentNotificationMessage?>(Revoked?null:new(user,null,"Document event","Authorized notification",e.DocumentId,"Info",null));}
    private sealed class Transport:IDocumentNotificationTransport{public int Calls;public Task<string> AcceptAsync(Guid key,DocumentNotificationMessage message,CancellationToken ct){Calls++;return Task.FromResult("Accepted");}}
}
