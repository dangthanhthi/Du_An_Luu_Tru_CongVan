using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace DocumentService;
// DAS-to-DAS protocol. A provisioned service JWT must carry NotificationSend.
// No user bearer is forwarded and no credential is written to logs.
public sealed class ConfiguredNotificationTransport(HttpClient http,IConfiguration config):IDocumentNotificationTransport
{
    public async Task<string> AcceptAsync(Guid key,DocumentNotificationMessage message,CancellationToken ct)
    {
        if(!config.GetValue<bool>("Notifications:TransportEnabled"))return "PendingConfiguration";
        var address=config["Notifications:Endpoint"];var token=config["Notifications:ServiceToken"];
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri)||uri.Scheme!="https"&&!uri.IsLoopback||!string.IsNullOrEmpty(uri.UserInfo)||string.IsNullOrWhiteSpace(token))return "PendingConfiguration";
        using var request=new HttpRequestMessage(HttpMethod.Post,uri){Content=JsonContent.Create(message)};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);request.Headers.Add("Idempotency-Key",key.ToString("N"));
        using var response=await http.SendAsync(request,ct);
        if(response.StatusCode==System.Net.HttpStatusCode.Accepted){
            JsonElement body;
            try {body=await response.Content.ReadFromJsonAsync<JsonElement>(ct);}catch(JsonException){return "RequiresReconciliation";}
            if(body.TryGetProperty("success",out var success)&&success.ValueKind==JsonValueKind.True&&body.TryGetProperty("data",out var data)&&data.TryGetProperty("id",out var id)&&id.TryGetGuid(out var accepted)&&accepted!=Guid.Empty)return "Accepted";
            return "RequiresReconciliation";
        }
        return (int)response.StatusCode>=500||response.StatusCode==System.Net.HttpStatusCode.TooManyRequests?"Retryable":"RequiresReconciliation";
    }
}
public sealed class DocumentNotificationWorker(IServiceScopeFactory scopes,ILogger<DocumentNotificationWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>();var service=scope.ServiceProvider.GetRequiredService<DocumentNotifications>();
                var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var deliveries=await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync(db.Set<DocumentNotificationDelivery>().Where(x=>(x.State=="Pending"||x.State=="Dispatching"||x.State=="Retryable"||x.State=="PendingConfiguration")&&x.LeaseUntilUnix<=now&&x.NextAttemptUnix<=now).OrderBy(x=>x.NextAttemptUnix).Take(100).Select(x=>x.Id),ct);
                foreach(var id in deliveries)await service.DispatchAsync(id,ct);
                // Audience planning is explicit per outbox event. The future provisioned adapter
                // invokes PlanAsync; do not rescan every historical event on each cycle.
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){log.LogWarning("Document notification cycle unavailable: {Type}",e.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(30),ct);
        }
    }
}
