using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;
public sealed class DocumentNotificationDelivery
{
    public Guid Id{get;set;}=Guid.NewGuid();public Guid EventId{get;set;}public Guid RecipientId{get;set;}
    public string State{get;set;}="Pending";public int Attempts{get;set;}public long NextAttemptUnix{get;set;}public long LeaseUntilUnix{get;set;}public Guid? LeaseToken{get;set;}
    public string PayloadJson{get;set;}="";public long Version{get;set;}=1;
}
public sealed record DocumentNotificationMessage(Guid RecipientUserId,string? RecipientEmail,string Subject,string Body,Guid? RelatedDocumentId,string NotificationType,string? ActionUrl);
// Adapter supplies active recipients AND current audience grants. No global role inference.
public interface IDocumentNotificationAudience
{
    Task<IReadOnlySet<Guid>> RecipientsAsync(DocumentOutboxEvent change,CancellationToken ct);
    Task<DocumentNotificationMessage?> ResolveAsync(DocumentOutboxEvent change,Guid recipient,CancellationToken ct);
}
public sealed class UnavailableDocumentNotificationAudience:IDocumentNotificationAudience
{
    public Task<IReadOnlySet<Guid>> RecipientsAsync(DocumentOutboxEvent change,CancellationToken ct)=>throw new DocumentRegistrationRuleException(503,"NOTIFICATION_AUDIENCE_UNAVAILABLE","Notification audience is not connected.");
    public Task<DocumentNotificationMessage?> ResolveAsync(DocumentOutboxEvent change,Guid recipient,CancellationToken ct)=>throw new DocumentRegistrationRuleException(503,"NOTIFICATION_AUDIENCE_UNAVAILABLE","Notification audience is not connected.");
}
public interface IDocumentNotificationTransport {Task<string> AcceptAsync(Guid key,DocumentNotificationMessage message,CancellationToken ct);}
public sealed class UnavailableDocumentNotificationTransport:IDocumentNotificationTransport
{public Task<string> AcceptAsync(Guid key,DocumentNotificationMessage message,CancellationToken ct)=>Task.FromResult("PendingConfiguration");}
public sealed class DocumentNotifications(DocumentDbContext db,IDocumentNotificationAudience audience,IDocumentNotificationTransport transport,TimeProvider clock)
{
    public async Task<int> PlanAsync(Guid eventId,CancellationToken ct)
    {
        var change=await db.DocumentOutboxEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==eventId&&(x.Type=="DocumentRegistered"||x.Type=="DocumentUpdated"||x.Type=="DocumentStatusChanged"),ct)??throw new DocumentRegistrationRuleException(404,"EVENT_NOT_FOUND","Event unavailable.");
        var recipients=await audience.RecipientsAsync(change,ct);
        if(recipients.Count>2000||recipients.Contains(Guid.Empty))throw new DocumentRegistrationRuleException(503,"NOTIFICATION_AUDIENCE_INVALID","Notification audience is inconsistent.");
        foreach(var recipient in recipients)
        {
            if(await db.Set<DocumentNotificationDelivery>().AnyAsync(x=>x.EventId==eventId&&x.RecipientId==recipient,ct))continue;
            var delivery=new DocumentNotificationDelivery{EventId=eventId,RecipientId=recipient};db.Add(delivery);
            try {await db.SaveChangesAsync(ct);}catch(DbUpdateException){db.Entry(delivery).State=EntityState.Detached;if(!await db.Set<DocumentNotificationDelivery>().AnyAsync(x=>x.EventId==eventId&&x.RecipientId==recipient,ct))throw;}
        }
        return recipients.Count;
    }
    public async Task<string> DispatchAsync(Guid id,CancellationToken ct)
    {
        var now=clock.GetUtcNow().ToUnixTimeSeconds();var token=Guid.NewGuid();
        // Acceptance is an idempotent durable protocol: retry the exact stored payload/key.
        var count=await db.Set<DocumentNotificationDelivery>().Where(x=>x.Id==id&&(x.State=="Pending"||x.State=="PendingConfiguration"||x.State=="Dispatching"||x.State=="Retryable")&&x.LeaseUntilUnix<=now&&x.NextAttemptUnix<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Dispatching").SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,now+60).SetProperty(x=>x.Attempts,x=>x.Attempts+1).SetProperty(x=>x.Version,x=>x.Version+1),ct);
        if(count!=1)return "NotClaimed";
        var delivery=await db.Set<DocumentNotificationDelivery>().AsNoTracking().SingleAsync(x=>x.Id==id,ct);string state;
        try {
            var change=await db.DocumentOutboxEvents.AsNoTracking().SingleAsync(x=>x.Id==delivery.EventId,ct);
            var message=await audience.ResolveAsync(change,delivery.RecipientId,ct);
            if(message is null)state="Suppressed";
            else if(message.RecipientUserId!=delivery.RecipientId||message.RelatedDocumentId!=change.DocumentId)throw new DocumentRegistrationRuleException(503,"NOTIFICATION_AUDIENCE_INVALID","Invalid notification projection.");
            else {
                var payload=JsonSerializer.Serialize(message);
                if(delivery.PayloadJson.Length>0&&delivery.PayloadJson!=payload)state="RequiresReconciliation";
                else {
                    var saved=await db.Set<DocumentNotificationDelivery>().Where(x=>x.Id==id&&x.LeaseToken==token&&x.LeaseUntilUnix>clock.GetUtcNow().ToUnixTimeSeconds()).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.PayloadJson,payload),ct);
                    if(saved!=1)return "LeaseLost";
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(20));
                    state=await transport.AcceptAsync(id,message,deadline.Token);
                    if(state is not ("Accepted" or "PendingConfiguration" or "Retryable"))state="RequiresReconciliation";
                }
            }
        }
        catch(Exception e) when(e is DocumentRegistrationRuleException or HttpRequestException or IOException or OperationCanceledException){state="Retryable";}
        if(state=="Retryable"&&delivery.Attempts>=5)state="DeadLetter";
        await db.Set<DocumentNotificationDelivery>().Where(x=>x.Id==id&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUnix,0L).SetProperty(x=>x.NextAttemptUnix,now+300).SetProperty(x=>x.Version,x=>x.Version+1),CancellationToken.None);
        return state;
    }
}
