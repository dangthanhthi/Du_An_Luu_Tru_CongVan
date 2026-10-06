using System.Net;
using Das.PdfProtocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
namespace FileService.Tests;
public sealed class PdfTransportTests
{
    [Theory][InlineData("missingKey")][InlineData("shortKey")][InlineData("httpProduction")][InlineData("userinfo")]
    public async Task Missing_or_insecure_credentials_never_send_a_machine_request(string invalid)
    {
        var handler=new Handler();using var http=new HttpClient(handler);using var client=new PdfFilesHttpClient(http,Config(invalid),new Env(invalid=="httpProduction"?"Production":"Development"));
        Assert.Equal(503,(await Assert.ThrowsAsync<PdfProtocolException>(()=>client.ActivateAsync(Guid.NewGuid(),default))).Status);Assert.Equal(0,handler.Sent);
    }
    [Theory][InlineData("oversized")][InlineData("invalidJson")][InlineData("redirect")][InlineData("unavailable")]
    public async Task Malformed_oversized_redirected_or_offline_response_is_never_a_successful_receipt(string mode)
    {
        var handler=new Handler{Response=mode switch {"redirect"=>new(HttpStatusCode.Redirect),"unavailable"=>new(HttpStatusCode.ServiceUnavailable),_=>new(HttpStatusCode.OK){Content=new StringContent(mode=="oversized"?new string('x',32769):"not JSON")}}};
        using var http=new HttpClient(handler);using var client=new PdfFilesHttpClient(http,Config("valid"),new Env("Development"));
        Assert.Equal(503,(await Assert.ThrowsAsync<PdfProtocolException>(()=>client.ActivateAsync(Guid.NewGuid(),default))).Status);Assert.Equal(1,handler.Sent);
    }
    private static IConfiguration Config(string invalid)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["PdfProtocol:Key"]=invalid=="missingKey"?null:invalid=="shortKey"?"short":"Transport-tests-only-machine-key-at-least-32-bytes",["PdfProtocol:Files"]=invalid=="userinfo"?"http://user:secret@localhost":"http://localhost"}).Build();
    private sealed class Handler:HttpMessageHandler{public int Sent;public HttpResponseMessage Response=new(HttpStatusCode.OK);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Sent++;return Task.FromResult(Response);}}
    private sealed class Env(string name):IHostEnvironment{public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="tests";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
