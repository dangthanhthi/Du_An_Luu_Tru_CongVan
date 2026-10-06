using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;
public sealed class ReminderFanoutManifest {public Guid BatchId{get;set;}public string PlanHash{get;set;}="";}
public sealed class ReminderDelivery
{
    public Guid Id{get;set;}=Guid.NewGuid();public Guid BatchId{get;set;}public Guid InputterUserId{get;set;}
    public string PayloadJson{get;set;}="";public string State{get;set;}="Pending";public int Attempts{get;set;}public int Failures{get;set;}
    public long LeaseUntilUnix{get;set;}public Guid? LeaseToken{get;set;}public long NextAttemptUnix{get;set;}public long Version{get;set;}=1;
    public Guid? NotificationId{get;set;}public string? NotificationState{get;set;}
}
public sealed record ReminderNotificationMessage(Guid RecipientUserId,string RecipientEmail,string Subject,string Body,Guid? RelatedDocumentId,string NotificationType,string? ActionUrl,IReadOnlyList<string> Cc);
public sealed record ReminderAcceptance(string State,Guid? ReceiptId=null,string? InboxState=null);
public interface IReminderNotificationTransport {Task<ReminderAcceptance> AcceptAsync(Guid id,ReminderNotificationMessage message,CancellationToken ct);}
public sealed class ConfiguredReminderNotificationTransport(HttpClient http,IConfiguration config):IReminderNotificationTransport
{
    public async Task<ReminderAcceptance> AcceptAsync(Guid id,ReminderNotificationMessage message,CancellationToken ct)
    {
        if(!config.GetValue<bool>("Reminders:TransportEnabled")||!config.GetValue<bool>("Notifications:TransportEnabled"))return new("PendingConfiguration");
        if(!Uri.TryCreate(config["Notifications:Endpoint"],UriKind.Absolute,out var uri)||uri.Scheme!="https"&&uri.Scheme!="http"||uri.Scheme!="https"&&!uri.IsLoopback||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||string.IsNullOrWhiteSpace(config["Notifications:ServiceToken"]))return new("PendingConfiguration");
        using var request=new HttpRequestMessage(HttpMethod.Post,uri){Content=JsonContent.Create(message)};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",config["Notifications:ServiceToken"]);request.Headers.Add("Idempotency-Key",id.ToString("N"));
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode!=HttpStatusCode.Accepted)return new((int)response.StatusCode>=500||response.StatusCode==HttpStatusCode.TooManyRequests?"Retryable":"RequiresReconciliation");
        try {
            if(response.Content.Headers.ContentLength>16384)return new("RequiresReconciliation");
            using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var chunk=new byte[4096];
            int read;while((read=await stream.ReadAsync(chunk,ct))>0){if(buffer.Length+read>16384)return new("RequiresReconciliation");buffer.Write(chunk,0,read);}
            using var parsed=JsonDocument.Parse(buffer.ToArray());var body=parsed.RootElement;
            if(body.ValueKind==JsonValueKind.Object&&body.TryGetProperty("success",out var ok)&&ok.ValueKind==JsonValueKind.True&&body.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.Object&&data.TryGetProperty("id",out var value)&&value.ValueKind==JsonValueKind.String&&value.TryGetGuid(out var receipt)&&receipt!=Guid.Empty&&data.TryGetProperty("state",out var state)&&state.ValueKind==JsonValueKind.String&&state.GetString() is "Queued" or "NoEmail" or "Sending" or "Sent" or "Retryable" or "PendingConfiguration" or "DeadLetter" or "UnknownOutcome")return new("Accepted",receipt,state.GetString());
        }catch(JsonException){ }
        return new("RequiresReconciliation");
    }
}
public sealed class DurableReminderTransport(DocumentDbContext db,IReminderNotificationTransport transport,TimeProvider clock,IConfiguration config):IReminderTransport
{
    public async Task<string> EnqueueAsync(Guid key,ReminderPlan plan,CancellationToken ct)
    {
        if(!config.GetValue<bool>("Reminders:TransportEnabled"))return "PendingConfiguration";
        var batch=await db.Set<ReminderBatch>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==key,ct);
        if(batch is null||batch.State!="Dispatching"||batch.Period!=WeeklyReminderSchedule.DuePeriod(clock.GetUtcNow()))return "RequiresReconciliation";
        var envelopes=Canonical(plan,batch.DepartmentId);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelopes))));
        var manifest=await db.Set<ReminderFanoutManifest>().AsNoTracking().SingleOrDefaultAsync(x=>x.BatchId==key,ct);
        if(manifest is null)
        {
            // One SaveChanges transaction commits the complete recipient set before HTTP.
            manifest=new(){BatchId=key,PlanHash=hash};var rows=envelopes.Select(x=>new ReminderDelivery{BatchId=key,InputterUserId=x.InputterUserId,PayloadJson=JsonSerializer.Serialize(Message(x))}).ToArray();db.Add(manifest);db.AddRange(rows);
            try {await db.SaveChangesAsync(ct);db.Entry(manifest).State=EntityState.Detached;foreach(var row in rows)db.Entry(row).State=EntityState.Detached;}
            catch(DbUpdateException){db.Entry(manifest).State=EntityState.Detached;foreach(var row in rows)db.Entry(row).State=EntityState.Detached;manifest=await db.Set<ReminderFanoutManifest>().AsNoTracking().SingleOrDefaultAsync(x=>x.BatchId==key,ct);if(manifest is null)throw;}
        }
        if(manifest.PlanHash!=hash)return "RequiresReconciliation";
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(20));var now=clock.GetUtcNow().ToUnixTimeSeconds();
        var ids=await db.Set<ReminderDelivery>().AsNoTracking().Where(x=>x.BatchId==key&&(x.State=="Pending"||x.State=="Retryable"||x.State=="PendingConfiguration"||x.State=="Dispatching")&&x.NextAttemptUnix<=now&&x.LeaseUntilUnix<=now).OrderBy(x=>x.Id).Take(20).Select(x=>x.Id).ToArrayAsync(ct);
        foreach(var id in ids){if(deadline.IsCancellationRequested)break;await Dispatch(id,deadline.Token);}
        var states=await db.Set<ReminderDelivery>().AsNoTracking().Where(x=>x.BatchId==key).Select(x=>x.State).ToArrayAsync(CancellationToken.None);
        if(states.Any(x=>x=="RequiresReconciliation"))return "RequiresReconciliation";
        if(states.Any(x=>x=="DeadLetter"))return "DeadLetter";
        return states.Length>0&&states.All(x=>x=="Accepted")?"Queued":"PendingAcceptance";
    }
    private async Task Dispatch(Guid id,CancellationToken ct)
    {
        var token=Guid.NewGuid();var now=clock.GetUtcNow().ToUnixTimeSeconds();
        var claim=await db.Set<ReminderDelivery>().Where(x=>x.Id==id&&(x.State=="Pending"||x.State=="Retryable"||x.State=="PendingConfiguration"||x.State=="Dispatching")&&x.LeaseUntilUnix<=now&&x.NextAttemptUnix<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Dispatching").SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,now+60).SetProperty(x=>x.Attempts,x=>x.Attempts+1).SetProperty(x=>x.Version,x=>x.Version+1),ct);
        if(claim!=1)return;
        var row=await db.Set<ReminderDelivery>().AsNoTracking().SingleAsync(x=>x.Id==id,ct);ReminderAcceptance result;
        try {using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));result=await transport.AcceptAsync(id,JsonSerializer.Deserialize<ReminderNotificationMessage>(row.PayloadJson)!,timeout.Token);}
        catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException){result=new("Retryable");}
        if(result.State is not ("Accepted" or "PendingConfiguration" or "Retryable")||result.State=="Accepted"&&(result.ReceiptId is null||result.ReceiptId==Guid.Empty))result=new("RequiresReconciliation");
        var failures=row.Failures+(result.State=="Retryable"?1:0);if(failures>=5&&result.State=="Retryable")result=new("DeadLetter");
        await db.Set<ReminderDelivery>().Where(x=>x.Id==id&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,result.State).SetProperty(x=>x.Failures,failures).SetProperty(x=>x.NotificationId,result.ReceiptId).SetProperty(x=>x.NotificationState,result.InboxState).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUnix,0L).SetProperty(x=>x.NextAttemptUnix,now+300).SetProperty(x=>x.Version,x=>x.Version+1),CancellationToken.None);
    }
    private static ReminderEnvelope[] Canonical(ReminderPlan plan,Guid department)
    {
        if(plan.Envelopes.Count==0||plan.Envelopes.Count>1000||plan.Envelopes.Any(x=>x.InputterUserId==Guid.Empty||x.Documents.Count==0)||plan.Envelopes.Select(x=>x.InputterUserId).Distinct().Count()!=plan.Envelopes.Count)throw Invalid();
        var documents=plan.Envelopes.SelectMany(x=>x.Documents).ToArray();if(documents.Length>1000||documents.Select(x=>x.DocumentId).Distinct().Count()!=documents.Length||documents.Any(x=>x.DocumentId==Guid.Empty||x.DepartmentId!=department||x.Version<1||x.Kind is not ("Internal" or "Outgoing")||x.Status is not ("InProgress" or "Distributed")))throw Invalid();
        return plan.Envelopes.OrderBy(x=>x.InputterUserId).Select(x=>{var to=Email(x.To);var cc=x.Cc.Select(Email).Where(y=>y!=to).Distinct().OrderBy(y=>y,StringComparer.Ordinal).ToArray();if(cc.Length>20)throw Invalid();return new ReminderEnvelope(x.InputterUserId,to,cc,x.Documents.OrderBy(d=>d.DocumentId).Select(d=>d with{RecipientList=d.RecipientList.OrderBy(y=>y,StringComparer.Ordinal).ToArray()}).ToArray());}).ToArray();
    }
    private static string Email(string value){if(value.Length>200||value.Any(char.IsControl)||!MailAddress.TryCreate(value,out var parsed)||!parsed.Address.Equals(value,StringComparison.OrdinalIgnoreCase))throw Invalid();return parsed.Address.ToLowerInvariant();}
    private static DocumentRegistrationRuleException Invalid()=>new(503,"REMINDER_PLAN_INVALID","Reminder projection is inconsistent.");
    private static ReminderNotificationMessage Message(ReminderEnvelope x)=>new(x.InputterUserId,x.To,"Nhắc công văn chưa hoàn tất",$"Bạn có {x.Documents.Count} công văn đi/nội bộ chưa hoàn tất, đã đăng ký quá 14 ngày. Đăng nhập DAS để kiểm tra hồ sơ trong phạm vi được cấp quyền.",null,"Warning",null,x.Cc);
}
