using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReminderFanoutTests
{
    [Fact] public async Task Configured_fanout_replays_after_new_scope_without_duplicate_receipts_or_modified_body()
    {
        await using var host=new V2HttpTests.Host();var handler=new Receiver();await using var configured=host.WithWebHostBuilder(b=>b.ConfigureServices(s=>{
            s.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddConfiguration(host.Services.GetRequiredService<IConfiguration>()).AddInMemoryCollection(new Dictionary<string,string?>{{"Reminders:TransportEnabled","true"},{"Notifications:TransportEnabled","true"},{"Notifications:Endpoint","http://localhost/api/notifications/send"},{"Notifications:ServiceToken","Test-only-service-token"}}).Build());
            s.AddHttpClient(nameof(IReminderNotificationTransport)).ConfigurePrimaryHttpMessageHandler(()=>handler);
        }));
        var id=Guid.NewGuid();var plan=Plan();
        using(var scope=configured.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>();db.Add(new ReminderBatch{Id=id,DepartmentId=plan.Envelopes[0].Documents[0].DepartmentId,Period=new(2026,10,5),State="Dispatching",PayloadJson=JsonSerializer.Serialize(plan),CreatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();Assert.Equal("Queued",await scope.ServiceProvider.GetRequiredService<IReminderTransport>().EnqueueAsync(id,plan,default));}
        using(var scope=configured.Services.CreateScope()){Assert.Equal("Queued",await scope.ServiceProvider.GetRequiredService<IReminderTransport>().EnqueueAsync(id,plan with{EvaluatedAt=plan.EvaluatedAt.AddMinutes(5)},default));}
        Assert.Single(handler.Payloads);Assert.Contains("leader@example.test",handler.Payloads.Values.Single());Assert.DoesNotContain("Secret subject",handler.Payloads.Values.Single());
        var sent=JsonSerializer.Deserialize<ReminderNotificationMessage>(handler.Payloads.Values.Single(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Contains("quá 7 ngày",sent.Body);
        using(var scope=configured.Services.CreateScope()){var changed=plan with{Envelopes=[plan.Envelopes[0] with{To="changed@example.test"}]};Assert.Equal("RequiresReconciliation",await scope.ServiceProvider.GetRequiredService<IReminderTransport>().EnqueueAsync(id,changed,default));}
        Assert.Single(handler.Payloads);
    }
    [Fact] public async Task Default_transport_does_not_attempt_HTTP()
    {await using var host=new V2HttpTests.Host();using var scope=host.Services.CreateScope();Assert.Equal("PendingConfiguration",await scope.ServiceProvider.GetRequiredService<IReminderTransport>().EnqueueAsync(Guid.NewGuid(),Plan(),default));}
    internal static ReminderPlan Plan()=>new(DateTimeOffset.Parse("2026-10-05T01:00:00Z"),[new(Guid.Parse("11111111-1111-4111-8111-111111111111"),"inputter@example.test",["leader@example.test"],[new(Guid.NewGuid(),"Internal",Guid.NewGuid(),"ADM",false,"26-09-0001/INT/HL/ADM",new(2026,9,1),null,Guid.NewGuid(),"InProgress",[],1)])],[]);
    private sealed class Receiver:HttpMessageHandler
    {public Dictionary<string,string> Payloads=[];protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Assert.Equal("Test-only-service-token",request.Headers.Authorization?.Parameter);var key=request.Headers.GetValues("Idempotency-Key").Single();var body=await request.Content!.ReadAsStringAsync(ct);if(Payloads.TryGetValue(key,out var old))Assert.Equal(old,body);else Payloads.Add(key,body);return new(HttpStatusCode.Accepted){Content=JsonContent.Create(new{success=true,data=new{id=Guid.NewGuid(),state="Queued"}})};}}
}
