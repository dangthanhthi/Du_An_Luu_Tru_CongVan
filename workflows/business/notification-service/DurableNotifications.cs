using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
namespace NotificationService.Services;

public sealed record DurableMessage(Guid? RecipientUserId,string? RecipientEmail,string Subject,string Body,Guid? RelatedDocumentId,string NotificationType,string? ActionUrl,IReadOnlyList<string>? Cc=null);
public sealed class NotificationRuleException(int status,string code):Exception(code){public int Status{get;}=status;public string Code{get;}=code;}
public sealed class DurableNotifications(NotificationDbContext db,IHubContext<NotificationHub>? hub=null,ILogger<DurableNotifications>? logger=null)
{
    public async Task<DeliveryInbox> AcceptAsync(Guid sender,string key,DurableMessage message,CancellationToken ct)
    {
        if(sender==Guid.Empty||string.IsNullOrWhiteSpace(key)||key.Length>128||key.Any(char.IsControl))throw new NotificationRuleException(400,"INVALID_IDEMPOTENCY_KEY");
        if(message.RecipientUserId==Guid.Empty||(message.RecipientUserId is null&&string.IsNullOrWhiteSpace(message.RecipientEmail))||string.IsNullOrWhiteSpace(message.Subject)||message.Subject.Length>250||string.IsNullOrWhiteSpace(message.Body)||message.Body.Length>1000||message.NotificationType is not ("Info" or "Urgent" or "Success" or "Warning")||message.RelatedDocumentId==Guid.Empty)throw new NotificationRuleException(400,"INVALID_NOTIFICATION");
        if(message.RecipientEmail is not null&&(message.RecipientEmail.Length>200||!System.Net.Mail.MailAddress.TryCreate(message.RecipientEmail,out var mail)||mail.Address!=message.RecipientEmail))throw new NotificationRuleException(400,"INVALID_EMAIL");
        if(message.ActionUrl is not null&&(!message.ActionUrl.StartsWith('/')||message.ActionUrl.StartsWith("//")||message.ActionUrl.Contains('\\')||message.ActionUrl.Length>500||message.ActionUrl.Any(char.IsControl)))throw new NotificationRuleException(400,"INVALID_ACTION_URL");
        if(message.Cc is not null&&(message.Cc.Count>20||message.Cc.Count>0&&message.RecipientEmail is null||message.Cc.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>200||!System.Net.Mail.MailAddress.TryCreate(x,out var address)||address.Address!=x)))throw new NotificationRuleException(400,"INVALID_CC");
        message=message with{Cc=message.Cc?.Where(x=>!x.Equals(message.RecipientEmail,StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray()};
        var payload=JsonSerializer.Serialize(message);var keyHash=Hash(key);var bodyHash=Hash(payload);
        var old=await db.Set<DeliveryInbox>().AsNoTracking().SingleOrDefaultAsync(x=>x.SenderId==sender&&x.KeyHash==keyHash,ct);
        if(old is not null)return Replay(old,bodyHash);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var pref=message.RecipientUserId is null?null:await db.UserNotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==message.RecipientUserId,ct);
        var suppressed=pref is not null&&pref.UrgentOnly&&message.NotificationType!="Urgent";
        var inbox=new DeliveryInbox{SenderId=sender,KeyHash=keyHash,BodyHash=bodyHash,PayloadJson=payload,State=message.RecipientEmail is null||suppressed||pref?.EmailEnabled==false?"NoEmail":"Queued"};db.Add(inbox);
        InAppNotification? notification=null;
        if(message.RecipientUserId is Guid user&&!suppressed&&pref?.InAppEnabled!=false){
            notification=new(){Id=inbox.Id,RecipientUserId=user,Title=message.Subject,Message=message.Body,ActionUrl=message.ActionUrl,RelatedDocumentId=message.RelatedDocumentId,NotificationType=message.NotificationType};db.Add(notification);}
        try {await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}
        catch(DbUpdateException){await tx.RollbackAsync(ct);db.ChangeTracker.Clear();var winner=await db.Set<DeliveryInbox>().AsNoTracking().SingleOrDefaultAsync(x=>x.SenderId==sender&&x.KeyHash==keyHash,ct);if(winner is null)throw;return Replay(winner,bodyHash);}
        // Realtime push is a hint; REST reads recover from lost pushes using the persisted ID.
        if(notification is not null&&hub is not null){try{await hub.Clients.Group($"user_{notification.RecipientUserId:D}").SendAsync("ReceiveNotification",notification,ct);}catch(Exception e){logger?.LogWarning("Notification push unavailable: {Type}",e.GetType().Name);}}
        return inbox;
    }
    private static DeliveryInbox Replay(DeliveryInbox existing,string hash)=>existing.BodyHash==hash?existing:throw new NotificationRuleException(409,"IDEMPOTENCY_CONFLICT");
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public interface IDeliveryEmail {Task<string> SendAsync(DurableMessage message,Guid messageId,CancellationToken ct);}
public sealed class DurableDelivery(NotificationDbContext db,IDeliveryEmail email,TimeProvider clock)
{
    public async Task<string> ProcessAsync(Guid id,CancellationToken ct)
    {
        var now=clock.GetUtcNow().ToUnixTimeSeconds();var token=Guid.NewGuid();
        // A crash after SMTP started has an unknown outcome. Never blindly resend.
        await db.Set<DeliveryInbox>().Where(x=>x.Id==id&&x.State=="Sending"&&x.LeaseUntilUnix<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"UnknownOutcome"),ct);
        var count=await db.Set<DeliveryInbox>().Where(x=>x.Id==id&&(x.State=="Queued"||x.State=="PendingConfiguration"||x.State=="Retryable")&&x.LeaseUntilUnix<=now&&x.NextAttemptUnix<=now)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Sending").SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,now+120).SetProperty(x=>x.Attempts,x=>x.Attempts+1).SetProperty(x=>x.Version,x=>x.Version+1),ct);
        if(count!=1)return "NotClaimed";
        var inbox=await db.Set<DeliveryInbox>().AsNoTracking().SingleAsync(x=>x.Id==id,ct);var message=JsonSerializer.Deserialize<DurableMessage>(inbox.PayloadJson)!;string state;
        try {
            var preferences=message.RecipientUserId is null?null:await db.UserNotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==message.RecipientUserId,ct);
            state=preferences?.EmailEnabled==false||preferences is not null&&preferences.UrgentOnly&&message.NotificationType!="Urgent"?"NoEmail":await email.SendAsync(message,id,ct);
        }
        catch(Exception e) when(e is IOException or HttpRequestException or OperationCanceledException){state="UnknownOutcome";}
        if(state is not ("Sent" or "NoEmail" or "Retryable" or "PendingConfiguration" or "UnknownOutcome"))state="UnknownOutcome";
        if(state=="Retryable"&&inbox.Attempts>=5)state="DeadLetter";
        var next=now+(state=="Retryable"?(long)Math.Min(3600,30*Math.Pow(2,inbox.Attempts)):300);
        await db.Set<DeliveryInbox>().Where(x=>x.Id==id&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUnix,0L).SetProperty(x=>x.NextAttemptUnix,next).SetProperty(x=>x.Version,x=>x.Version+1),CancellationToken.None);
        return state;
    }
}
public sealed class DurableNotificationWorker(IServiceScopeFactory scopes,ILogger<DurableNotificationWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try {using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();var delivery=scope.ServiceProvider.GetRequiredService<DurableDelivery>();
                var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var ids=await db.Set<DeliveryInbox>().AsNoTracking().Where(x=>(x.State=="Queued"||x.State=="Retryable"||x.State=="PendingConfiguration"||x.State=="Sending")&&x.NextAttemptUnix<=now&&x.LeaseUntilUnix<=now).OrderBy(x=>x.NextAttemptUnix).ThenBy(x=>x.CreatedAt).Take(100).Select(x=>x.Id).ToArrayAsync(ct);foreach(var id in ids)await delivery.ProcessAsync(id,ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){log.LogWarning("Notification delivery unavailable: {Type}",e.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(30),ct);
        }
    }
}
