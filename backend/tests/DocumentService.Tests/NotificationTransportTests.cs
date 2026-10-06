using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace DocumentService.Tests;
public sealed class NotificationTransportTests
{
    [Fact] public async Task Disabled_or_insecure_transport_does_not_make_network_requests()
    {var handler=new Handler();using var client=new HttpClient(handler);var config=new ConfigurationBuilder().AddInMemoryCollection().Build();Assert.Equal("PendingConfiguration",await new ConfiguredNotificationTransport(client,config).AcceptAsync(Guid.NewGuid(),Message(),default));config["Notifications:TransportEnabled"]="true";config["Notifications:Endpoint"]="http://external.example.test/api/notifications/send";config["Notifications:ServiceToken"]="test-token";Assert.Equal("PendingConfiguration",await new ConfiguredNotificationTransport(client,config).AcceptAsync(Guid.NewGuid(),Message(),default));Assert.Equal(0,handler.Calls);}
    [Theory][InlineData("accepted","Accepted")][InlineData("malformed","RequiresReconciliation")][InlineData("redirect","RequiresReconciliation")][InlineData("conflict","RequiresReconciliation")][InlineData("unavailable","Retryable")]
    public async Task Only_durable_acceptance_counts_and_the_key_is_preserved(string response,string expected)
    {var handler=new Handler{Response=response};using var client=new HttpClient(handler);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Notifications:TransportEnabled","true"},{"Notifications:Endpoint","https://configured.example.test/api/notifications/send"},{"Notifications:ServiceToken","Test-only-token"}}).Build();var key=Guid.NewGuid();Assert.Equal(expected,await new ConfiguredNotificationTransport(client,config).AcceptAsync(key,Message(),default));Assert.Equal(key.ToString("N"),handler.Key);Assert.Equal("Test-only-token",handler.Token);}
    private static DocumentNotificationMessage Message()=>new(Guid.NewGuid(),null,"Test","Body",Guid.NewGuid(),"Info",null);
    private sealed class Handler:HttpMessageHandler
    {public int Calls;public string Response="accepted";public string? Key,Token;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct){Calls++;Key=req.Headers.GetValues("Idempotency-Key").Single();Token=req.Headers.Authorization?.Parameter;return Task.FromResult<HttpResponseMessage>(Response switch{"malformed"=>new(HttpStatusCode.Accepted){Content=new StringContent("bad")},"redirect"=>new(HttpStatusCode.TemporaryRedirect),"conflict"=>new(HttpStatusCode.Conflict),"unavailable"=>new(HttpStatusCode.ServiceUnavailable),_=>new(HttpStatusCode.Accepted){Content=JsonContent.Create(new{success=true,data=new{id=Guid.NewGuid(),state="Queued"}})}});}}
}
