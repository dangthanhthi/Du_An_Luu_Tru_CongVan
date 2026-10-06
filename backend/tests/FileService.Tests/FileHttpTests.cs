using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FilesService.Data;
using FilesService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace FileService.Tests;

public sealed class FileHttpTests
{
    [Fact]
    public async Task Anonymous_liveness_exposes_only_service_status_and_keeps_file_metadata_protected()
    {
        await using var f=new Host();using var client=f.CreateClient();
        var response=await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var text=await response.Content.ReadAsStringAsync();using var json=JsonDocument.Parse(text);
        Assert.Equal("healthy",json.RootElement.GetProperty("status").GetString());
        Assert.Equal("files-service",json.RootElement.GetProperty("service").GetString());
        Assert.Equal(2,json.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain(f.Root,text);Assert.DoesNotContain(Key,text);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/files/{Guid.NewGuid()}/info")).StatusCode);
    }
    [Fact]
    public async Task Anonymous_upload_info_and_content_are_401_and_public_dev_token_route_is_absent()
    {
        await using var f=new Host();using var client=f.CreateClient();var id=Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/files/upload",Form())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/files/{id}/info")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/files/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/dev/token")).StatusCode);
    }
    [Theory]
    [InlineData("expired")][InlineData("badSignature")][InlineData("noGuid")][InlineData("emptyGuid")]
    public async Task Invalid_signed_or_subject_tokens_cannot_upload(string invalid)
    {
        await using var f=new Host();using var client=f.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",Token(invalid=="noGuid"?"not-guid":invalid=="emptyGuid"?Guid.Empty.ToString():Guid.NewGuid().ToString(),invalid));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/files/upload",Form())).StatusCode);
    }
    [Fact]
    public async Task Production_default_scanner_gives_202_pending_metadata_without_content_or_physical_path()
    {
        await using var f=new Host();var actor=Guid.NewGuid();using var client=Client(f,actor);var response=await client.PostAsync("/api/files/upload",Form());
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);var text=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("storagePath",text,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(f.Root,text);
        using var json=JsonDocument.Parse(text);var data=json.RootElement.GetProperty("data");var id=data.GetProperty("id").GetGuid();Assert.Equal("PendingScan",data.GetProperty("state").GetString());Assert.False(data.GetProperty("canDownload").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/files/{id}/info")).StatusCode);Assert.Equal((HttpStatusCode)423,(await client.GetAsync($"/api/files/{id}")).StatusCode);
        using var outsider=Client(f,Guid.NewGuid(),admin:true);Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/api/files/{id}/info")).StatusCode);
    }
    [Fact]
    public async Task Trusted_scan_available_supports_range_inline_etag_and_reauthorizes_conditional_requests()
    {
        await using var f=new Host(new PdfStorageTests.Scanner());var actor=Guid.NewGuid();using var client=Client(f,actor);var id=await Upload(client);
        using var request=new HttpRequestMessage(HttpMethod.Get,$"/api/files/{id}");request.Headers.Range=new(0,9);var response=await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent,response.StatusCode);Assert.Equal(PdfStorageTests.Pdf().Take(10),await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf",response.Content.Headers.ContentType!.MediaType);Assert.Equal("inline",response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("private",response.Headers.CacheControl!.ToString());Assert.Contains("no-store",response.Headers.CacheControl.ToString());Assert.Equal("nosniff",response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.NotNull(response.Headers.ETag);Assert.Equal(66,response.Headers.ETag!.Tag.Length);
        using var conditional=new HttpRequestMessage(HttpMethod.Get,$"/api/files/{id}");conditional.Headers.IfNoneMatch.Add(response.Headers.ETag);
        Assert.Equal(HttpStatusCode.NotModified,(await client.SendAsync(conditional)).StatusCode);
        using var outsider=Client(f,Guid.NewGuid(),true);outsider.DefaultRequestHeaders.IfNoneMatch.Add(response.Headers.ETag);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/api/files/{id}")).StatusCode);
        using var anonymous=f.CreateClient();anonymous.DefaultRequestHeaders.IfNoneMatch.Add(response.Headers.ETag);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/api/files/{id}")).StatusCode);
    }
    [Fact]
    public async Task Document_binding_revokes_unlinked_owner_access_until_document_policy_is_integrated()
    {
        await using var f=new Host(new PdfStorageTests.Scanner());var actor=Guid.NewGuid();using var client=Client(f,actor);var id=await Upload(client);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<FileDbContext>();var ticket=await db.Set<FilesService.Models.Entities.PdfUpload>().SingleAsync();ticket.DocumentId=Guid.NewGuid();await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/files/{id}/info")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/files/{id}")).StatusCode);
    }
    [Fact]
    public async Task Foreign_cors_origin_is_not_reflected_for_credentialed_requests()
    {
        await using var f=new Host();using var client=Client(f,Guid.NewGuid());using var request=new HttpRequestMessage(HttpMethod.Get,$"/api/files/{Guid.NewGuid()}/info");request.Headers.Add("Origin","https://untrusted.example");
        Assert.False((await client.SendAsync(request)).Headers.Contains("Access-Control-Allow-Origin"));
    }
    [Fact]
    public async Task Upload_rejects_multiple_files_instead_of_silently_selecting_the_first()
    {
        await using var f=new Host();using var client=Client(f,Guid.NewGuid());using var form=Form();form.Add(new ByteArrayContent(PdfStorageTests.Pdf()),"file","second.pdf");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/files/upload",form)).StatusCode);
        using var scope=f.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<FileDbContext>().Files.ToListAsync());
    }
    [Theory]
    [InlineData("missingKey")][InlineData("missingStorage")]
    public void Startup_never_uses_fallback_key_or_storage(string invalid)
    {
        using var host=new Host(invalid:invalid);Assert.ThrowsAny<Exception>(()=>host.CreateClient());
    }
    internal static async Task<Guid> Upload(HttpClient client){var response=await client.PostAsync("/api/files/upload",Form());Assert.Equal(HttpStatusCode.OK,response.StatusCode);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();}
    internal static MultipartFormDataContent Form(){var form=new MultipartFormDataContent();var bytes=new ByteArrayContent(PdfStorageTests.Pdf());bytes.Headers.ContentType=new("application/pdf");form.Add(bytes,"file","Báo cáo.pdf");return form;}
    private const string Key="Test-only-file-service-jwt-signing-key-with-64-characters-never-live";
    private static string Token(string actor,string? invalid=null,bool admin=false){var token=new JwtSecurityToken(claims:admin?[new("sub",actor),new(ClaimTypes.Role,"Admin")]:[new("sub",actor)],
        expires:invalid=="expired"?DateTime.UtcNow.AddMinutes(-5):DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(invalid=="badSignature"?Key+"wrong":Key)),SecurityAlgorithms.HmacSha256));return new JwtSecurityTokenHandler().WriteToken(token);}
    private static HttpClient Client(Host f,Guid actor,bool admin=false){var client=f.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",Token(actor.ToString(),admin:admin));return client;}
    private sealed class Host:WebApplicationFactory<Program>
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"das-file-http-"+Guid.NewGuid().ToString("N"));private readonly IPdfThreatScanner? scanner;private readonly string? invalid;
        public Host(IPdfThreatScanner? scanner=null,string? invalid=null){this.scanner=scanner;this.invalid=invalid;Directory.CreateDirectory(Root);}
        protected override void ConfigureWebHost(IWebHostBuilder builder){builder.UseEnvironment("Development");builder.UseSetting("Database:Provider","Sqlite");builder.UseSetting("Database:Initialize","true");
            builder.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(Root,"files.db"));builder.UseSetting("Storage:Path",invalid=="missingStorage"?"":Root);builder.UseSetting("Jwt:Secret",invalid=="missingKey"?"":Key);
            if(scanner is not null)builder.ConfigureServices(s=>{s.RemoveAll<IPdfThreatScanner>();s.AddSingleton(scanner);});}
        public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(Root))Directory.Delete(Root,true);}
        protected override void Dispose(bool disposing){base.Dispose(disposing);Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
}
