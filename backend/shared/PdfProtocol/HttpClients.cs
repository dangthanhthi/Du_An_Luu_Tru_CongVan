using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
namespace Das.PdfProtocol;

public abstract class PdfHttpClient(HttpClient http,IConfiguration config,IHostEnvironment environment,string target):IDisposable
{
    public void Dispose()=>http.Dispose();
    protected async Task<T> Send<T>(HttpMethod method,string path,object? body,CancellationToken ct,string? bearer=null)
    {
        var key=config["PdfProtocol:Key"];var address=config["PdfProtocol:"+target];
        if(string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key)<32 || key.Length>1024 ||
            !Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0 ||
            (uri.Scheme!="https" && !(environment.IsDevelopment() && uri.Scheme=="http")))
            throw new PdfProtocolException(503,"PDF_PROTOCOL_UNCONFIGURED");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request=new HttpRequestMessage(method,new Uri(uri,path));request.Headers.Add("X-DAS-Pdf-Key",key);
        if(bearer is not null)request.Headers.Authorization=AuthenticationHeaderValue.Parse(bearer);
        if(body is not null)request.Content=JsonContent.Create(body);
        try {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(!response.IsSuccessStatusCode)throw new PdfProtocolException(response.StatusCode is HttpStatusCode.NotFound?404:
                response.StatusCode is HttpStatusCode.Conflict?409:response.StatusCode is HttpStatusCode.Locked?423:503,"PDF_REMOTE_UNAVAILABLE");
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var bytes=new MemoryStream();var buffer=new byte[4096];
            while(true){var count=await stream.ReadAsync(buffer,timeout.Token);if(count==0)break;if(bytes.Length+count>32768)throw new PdfProtocolException(503,"PDF_INVALID_RESPONSE");bytes.Write(buffer,0,count);}
            return JsonSerializer.Deserialize<T>(bytes.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new PdfProtocolException(503,"PDF_INVALID_RESPONSE");
        } catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new PdfProtocolException(503,"PDF_REMOTE_TIMEOUT");}
        catch(Exception e) when(e is HttpRequestException or JsonException or IOException){throw new PdfProtocolException(503,"PDF_REMOTE_UNAVAILABLE");}
    }
}
public sealed class PdfFilesHttpClient(HttpClient http,IConfiguration config,IHostEnvironment environment):PdfHttpClient(http,config,environment,"Files"),IPdfFilesClient
{
    public Task<PdfReceipt> PrepareAsync(PdfPrepare r,CancellationToken ct)=>Send<PdfReceipt>(HttpMethod.Post,"/internal/pdf/prepare",r,ct);
    public Task<PdfReceipt> ActivateAsync(Guid id,CancellationToken ct)=>Send<PdfReceipt>(HttpMethod.Post,$"/internal/pdf/{id}/activate",null,ct);
    public async Task RetireAsync(Guid id,CancellationToken ct)=>_ = await Send<Dictionary<string,bool>>(HttpMethod.Post,$"/internal/pdf/{id}/retire",null,ct);
    public Task<PdfReceipt> InspectAsync(Guid id,CancellationToken ct)=>Send<PdfReceipt>(HttpMethod.Get,$"/internal/pdf/{id}/info",null,ct);
}
public sealed class PdfDocumentHttpClient(HttpClient http,IConfiguration config,IHostEnvironment environment,IHttpContextAccessor context):PdfHttpClient(http,config,environment,"Documents"),IPdfDocumentClient
{
    public Task<PdfOperation> OperationAsync(Guid id,CancellationToken ct)=>Send<PdfOperation>(HttpMethod.Get,$"/internal/pdf/{id}/operation",null,ct);
    public async Task<bool> CanReadAsync(Guid doc,Guid file,Guid op,Guid user,CancellationToken ct)
    {
        var bearer=context.HttpContext?.Request.Headers.Authorization.ToString();
        if(string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))throw new PdfProtocolException(503,"USER_AUTH_UNAVAILABLE");
        try {var response=await Send<ReadDecision>(HttpMethod.Get,$"/internal/pdf/{op}/read?documentId={doc}&fileId={file}",null,ct,bearer);return response.Allowed;}
        catch(PdfProtocolException e) when(e.Status==404){return false;}
    }
    private sealed record ReadDecision(bool Allowed);
}
