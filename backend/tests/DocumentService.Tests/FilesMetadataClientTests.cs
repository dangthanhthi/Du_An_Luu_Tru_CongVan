using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace DocumentService.Tests;

public sealed class FilesMetadataClientTests
{
    [Theory]
    [InlineData("PendingScan")][InlineData("Missing")][InlineData("Receiving")][InlineData("legacy")]
    [InlineData("unreadable")][InlineData("mime")][InlineData("size")][InlineData("hash")][InlineData("wrongId")][InlineData("failedEnvelope")][InlineData("bound")]
    public async Task Metadata_existence_does_not_make_a_pending_or_unverified_file_attachable(string invalid)
    {
        var id=Guid.NewGuid();using var http=new HttpClient(new Handler(_=>Response(id,invalid))){BaseAddress=new("http://files.local")};
        Assert.Null(await new FilesServiceClient(http).GetFileByIdAsync(id));
    }
    [Fact]
    public async Task Available_readable_pdf_with_matching_id_size_and_sha256_is_returned()
    {
        var id=Guid.NewGuid();using var http=new HttpClient(new Handler(_=>Response(id,null))){BaseAddress=new("http://files.local")};
        var result=await new FilesServiceClient(http).GetFileByIdAsync(id);Assert.NotNull(result);Assert.Equal(id,result.Id);Assert.Equal("application/pdf",result.ContentType);
    }
    [Fact]
    public async Task Unavailable_remote_files_service_never_becomes_successful_metadata()
    {
        using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.ServiceUnavailable))){BaseAddress=new("http://files.local")};
        Assert.Null(await new FilesServiceClient(http).GetFileByIdAsync(Guid.NewGuid()));
    }
    private static HttpResponseMessage Response(Guid id,string? invalid)=>new(HttpStatusCode.OK){Content=JsonContent.Create(new {
        success=invalid!="failedEnvelope",data=new {id=invalid=="wrongId"?Guid.NewGuid():id,originalName="test.pdf",sizeBytes=invalid=="size"?0:123,
            contentType=invalid=="mime"?"text/html":"application/pdf",state=invalid=="legacy"?null:invalid is "PendingScan" or "Missing" or "Receiving"?invalid:"Available",
            canDownload=invalid!="unreadable",canAttach=invalid!="bound",sha256=invalid=="hash"?new string('G',64):new string('A',64)},message=(string?)null,errors=Array.Empty<string>()})};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(response(request)); }
}
