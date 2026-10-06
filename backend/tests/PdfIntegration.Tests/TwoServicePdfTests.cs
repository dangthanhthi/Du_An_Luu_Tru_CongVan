extern alias doc;
using D=doc::DocumentService;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Das.PdfProtocol;
using FilesService.Data;
using FilesService.Models.Entities;
using FilesService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using UglyToad.PdfPig.Writer;
using Xunit;
namespace PdfIntegration.Tests;

public sealed class TwoServicePdfTests
{
    [Fact]
    public async Task Real_http_two_service_claim_activation_content_range_and_revocation()
    {
        await using var f=new Pair();var id=await f.Register();var file=await f.Upload();var op=await f.Replace(id,file,1);
        Assert.Equal(HttpStatusCode.NotFound,(await f.FilesClient.GetAsync($"/api/files/{file}")).StatusCode);
        await f.Dispatch();var info=await f.DocClient.GetAsync($"/api/v2/documents/{id}/pdf");Assert.Equal(HttpStatusCode.OK,info.StatusCode);
        using var range=new HttpRequestMessage(HttpMethod.Get,$"/api/files/{file}");range.Headers.Range=new(0,9);
        var response=await f.FilesClient.SendAsync(range);Assert.Equal(HttpStatusCode.PartialContent,response.StatusCode);Assert.NotNull(response.Headers.ETag);
        f.Authority.Readable=false;f.FilesClient.DefaultRequestHeaders.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotFound,(await f.FilesClient.GetAsync($"/api/files/{file}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.DocClient.GetAsync($"/api/v2/documents/{id}/pdf")).StatusCode);
        f.Authority.Offline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.FilesClient.GetAsync($"/api/files/{file}")).StatusCode);
        Assert.DoesNotContain("storagePath",await info.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Second_replacement_removes_old_bytes_only_after_activation_and_updates_last_editor()
    {
        await using var f=new Pair();var id=await f.Register();var first=await f.Upload();await f.Replace(id,first,1);await f.Dispatch();var second=await f.Upload();
        await f.Replace(id,second,2);Assert.True(File.Exists(Path.Combine(f.Files.Root,first.ToString("N")+".pdf")));
        await f.Dispatch();await f.Dispatch();Assert.False(File.Exists(Path.Combine(f.Files.Root,first.ToString("N")+".pdf")));
        Assert.Equal(HttpStatusCode.NotFound,(await f.FilesClient.GetAsync($"/api/files/{first}/info")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.FilesClient.GetAsync($"/api/files/{second}")).StatusCode);
        using var scope=f.Documents.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>();
        Assert.Equal(3,(await db.DocumentRegistrations.SingleAsync()).Version);Assert.Equal(f.User,(await db.DocumentRegistrations.SingleAsync()).LastModifierUserId);
        Assert.Equal(2,await db.Set<D.DocumentEditAudit>().CountAsync());Assert.Single(await db.Set<D.DocumentCurrentPdf>().ToListAsync());
    }
    [Fact]
    public async Task Actual_audit_failure_keeps_old_link_and_bytes_then_aborted_preparation_is_reconciled()
    {
        var fault=new AuditFault();await using var f=new Pair(fault:fault);var id=await f.Register();var first=await f.Upload();await f.Replace(id,first,1);await f.Dispatch();
        var second=await f.Upload();fault.Enabled=true;
        var response=await f.DocClient.PutAsJsonAsync($"/api/v2/documents/{id}/pdf",new D.PdfReplaceDraft(Guid.NewGuid(),second,2));Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.FilesClient.GetAsync($"/api/files/{first}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await f.FilesClient.GetAsync($"/api/files/{second}")).StatusCode);
        fault.Enabled=false;
        using(var scope=f.Documents.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>();var pending=await db.Set<D.PdfReplacement>().SingleAsync(x=>x.State=="Preparing");pending.CreatedAt=DateTime.UtcNow.AddHours(-3);await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<D.CurrentPdfService>().ExpirePreparingAsync();Assert.Equal(first,(await db.Set<D.DocumentCurrentPdf>().SingleAsync()).FileId);
        }
        using(var scope=f.Files.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<PdfClaims>().ReconcileAsync();
        Assert.False(File.Exists(Path.Combine(f.Files.Root,second.ToString("N")+".pdf")));Assert.True(File.Exists(Path.Combine(f.Files.Root,first.ToString("N")+".pdf")));
    }
    [Fact]
    public async Task Current_bytes_tampering_blocks_content_and_refreshes_document_completeness_projection()
    {
        await using var f=new Pair();var id=await f.Register();var file=await f.Upload();await f.Replace(id,file,1);await f.Dispatch();
        await File.WriteAllTextAsync(Path.Combine(f.Files.Root,file.ToString("N")+".pdf"),"corrupted");
        Assert.Equal(HttpStatusCode.NotFound,(await f.FilesClient.GetAsync($"/api/files/{file}")).StatusCode);
        using var scope=f.Documents.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<D.CurrentPdfService>().RefreshReadinessAsync();
        Assert.Equal("Missing",(await scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>().Set<D.DocumentCurrentPdf>().SingleAsync()).State);
        Assert.Equal(HttpStatusCode.Locked,(await f.DocClient.GetAsync($"/api/v2/documents/{id}/pdf")).StatusCode);
    }
    [Fact]
    public async Task Both_internal_services_require_separate_machine_key_and_public_routes_require_user()
    {
        await using var f=new Pair();using var anonymous=f.Files.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/internal/pdf/{Guid.NewGuid()}/info")).StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-DAS-Pdf-Key","wrong");Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/internal/pdf/{Guid.NewGuid()}/info")).StatusCode);
        using var docAnonymous=f.Documents.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await docAnonymous.GetAsync($"/api/v2/documents/{Guid.NewGuid()}/pdf")).StatusCode);
        docAnonymous.DefaultRequestHeaders.Add("X-DAS-Pdf-Key",MachineKey);Assert.Equal(HttpStatusCode.Unauthorized,(await docAnonymous.GetAsync($"/internal/pdf/{Guid.NewGuid()}/read?documentId={Guid.NewGuid()}&fileId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.DocClient.GetAsync($"/internal/pdf/{Guid.NewGuid()}/operation")).StatusCode);
    }
    [Fact]
    public async Task Default_authority_fails_closed_even_for_authenticated_admin()
    {
        await using var f=new Pair(authority:false);var id=await f.Register();var file=await f.Upload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.DocClient.PutAsJsonAsync($"/api/v2/documents/{id}/pdf",new D.PdfReplaceDraft(Guid.NewGuid(),file,1))).StatusCode);
        using var scope=f.Documents.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>().Set<D.PdfReplacement>().ToListAsync());
    }
    [Fact]
    public async Task Unconfigured_machine_key_has_no_default_and_never_mints_a_token()
    {
        await using var f=new Pair(machineKey:"");using var client=f.Files.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync($"/internal/pdf/{Guid.NewGuid()}/info")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/dev/token")).StatusCode);
    }

    private const string JwtKey="Two-service-tests-only-JWT-signing-key-never-used-in-runtime";
    [Fact]
    public async Task Completion_requires_available_current_pdf_and_distributed_issued_document_and_ready_replay_is_success()
    {
        await using var f=new Pair();var id=await f.Register();var file=await f.Upload();var op=await f.Replace(id,file,1);await f.Dispatch();
        var replay=await f.DocClient.PutAsJsonAsync($"/api/v2/documents/{id}/pdf",new D.PdfReplaceDraft(op,file,1));Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
        using(var scope=f.Documents.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>();var h=await db.DocumentRegistrations.SingleAsync();h.IssuedDate=DateOnly.FromDateTime(DateTime.UtcNow);await db.SaveChangesAsync();
            await new D.V2DocumentLifecycle(db,TimeProvider.System).ChangeAsync(id,new(h.Version,D.V2StatusAction.Distribute),await f.Authority.EditorAsync(id,f.User,default));
        }
        var response=await f.DocClient.GetAsync($"/api/v2/documents/{id}/pdf");Assert.Equal(HttpStatusCode.OK,response.StatusCode);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("data").GetProperty("completion").GetProperty("isComplete").GetBoolean());
        using var metadata=JsonDocument.Parse(await (await f.FilesClient.GetAsync($"/api/files/{file}/info")).Content.ReadAsStringAsync());Assert.False(metadata.RootElement.GetProperty("data").GetProperty("canAttach").GetBoolean());
    }
    private const string MachineKey="Separate-two-service-tests-only-machine-key-never-live";
    private sealed class Authority(Guid user,Guid department):D.IPdfAuthority
    {
        public bool Readable=true;public bool Offline;
        public Task<D.V2EditorActor> EditorAsync(Guid docId,Guid id,CancellationToken ct)=>Offline?throw new D.DocumentRegistrationRuleException(503,"OFFLINE","offline"):
            Task.FromResult(new D.V2EditorActor(id,id==user,new HashSet<Guid>{department},new HashSet<Guid>(),new HashSet<Guid>()));
        public Task<bool> CanReadAsync(Guid docId,Guid id,CancellationToken ct)=>Offline?throw new D.DocumentRegistrationRuleException(503,"OFFLINE","offline"):Task.FromResult(id==user&&Readable);
    }
    private sealed class AuditFault:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){
            if(Enabled && data.Context!.ChangeTracker.Entries<D.DocumentEditAudit>().Any(x=>x.State==EntityState.Added))throw new IOException("Injected PDF audit failure");return ValueTask.FromResult(result);}
    }
    private sealed class Pair:IAsyncDisposable
    {
        public readonly Guid User=Guid.NewGuid();public readonly Guid Department=Guid.NewGuid();public readonly Authority Authority;
        public readonly FileHost Files;public readonly DocumentHost Documents;public readonly HttpClient FilesClient;public readonly HttpClient DocClient;
        public Pair(bool authority=true,string machineKey=MachineKey,AuditFault? fault=null){Authority=new(User,Department);Files=new(machineKey);Documents=new(Files,authority?Authority:null,machineKey,fault);Files.Documents=Documents;
            FilesClient=Client(Files.CreateClient(),User);DocClient=Client(Documents.CreateClient(),User);}
        public async Task<Guid> Register(){using var scope=Documents.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<D.DocumentDbContext>();var d=await new D.V2RegistrationService(db,TimeProvider.System).RegisterAsync(
            new("INTERNAL","HL","PDF integration",User,Department),new(User,User,Department,"ADM","Administration"),Guid.NewGuid().ToString());return d.Id;}
        public async Task<Guid> Upload(){var builder=new PdfDocumentBuilder();builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(builder.Build()),"file","test.pdf");
            var r=await FilesClient.PostAsync("/api/files/upload",form);Assert.Equal(HttpStatusCode.OK,r.StatusCode);using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();}
        public async Task<Guid> Replace(Guid id,Guid file,long version){var op=Guid.NewGuid();var r=await DocClient.PutAsJsonAsync($"/api/v2/documents/{id}/pdf",new D.PdfReplaceDraft(op,file,version));Assert.Equal(HttpStatusCode.Accepted,r.StatusCode);return op;}
        public async Task Dispatch(){using var scope=Documents.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<D.CurrentPdfService>().DispatchAsync();}
        private static HttpClient Client(HttpClient client,Guid id){var token=new JwtSecurityToken(claims:[new("sub",id.ToString()),new(ClaimTypes.Role,"Admin")],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;}
        public async ValueTask DisposeAsync(){DocClient.Dispose();FilesClient.Dispose();await Documents.DisposeAsync();await Files.DisposeAsync();}
    }
    private sealed class FileHost(string machineKey):WebApplicationFactory<Program>
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"das-pdf-pair-files-"+Guid.NewGuid().ToString("N"));public DocumentHost Documents=null!;
        protected override void ConfigureWebHost(IWebHostBuilder b){Directory.CreateDirectory(Root);Settings(b,Root,machineKey);b.UseSetting("Storage:Path",Root);b.UseSetting("PdfProtocol:Documents","http://localhost");
            b.ConfigureServices(s=>{s.RemoveAll<IPdfThreatScanner>();s.AddSingleton<IPdfThreatScanner,Clean>();s.RemoveAll<IPdfDocumentClient>();s.AddScoped<IPdfDocumentClient>(sp=>new PdfDocumentHttpClient(Documents.CreateClient(),sp.GetRequiredService<IConfiguration>(),sp.GetRequiredService<IHostEnvironment>(),sp.GetRequiredService<IHttpContextAccessor>()));});}
        private sealed class Clean:IPdfThreatScanner{public Task<PdfScanResult> ScanAsync(Stream stream,CancellationToken ct)=>Task.FromResult(PdfScanResult.Clean);}
        public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private sealed class DocumentHost(FileHost files,Authority? authority,string machineKey,AuditFault? fault):WebApplicationFactory<doc::Program>
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"das-pdf-pair-docs-"+Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder b){Directory.CreateDirectory(root);Settings(b,root,machineKey);b.UseSetting("PdfProtocol:Files","http://localhost");
            b.ConfigureServices(s=>{if(authority is not null){s.RemoveAll<D.IPdfAuthority>();s.AddSingleton<D.IPdfAuthority>(authority);}s.RemoveAll<IPdfFilesClient>();s.AddScoped<IPdfFilesClient>(sp=>new PdfFilesHttpClient(files.CreateClient(),sp.GetRequiredService<IConfiguration>(),sp.GetRequiredService<IHostEnvironment>()));
                if(fault is not null){s.RemoveAll<DbContextOptions<D.DocumentDbContext>>();s.AddDbContext<D.DocumentDbContext>(o=>o.UseSqlite("Data Source="+Path.Combine(root,"test.db")).AddInterceptors(fault));}});}
        public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static void Settings(IWebHostBuilder b,string root,string key){b.UseEnvironment("Development");b.UseSetting("Database:Provider","Sqlite");b.UseSetting("Database:Initialize","true");b.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));b.UseSetting("Jwt:Secret",JwtKey);b.UseSetting("PdfProtocol:Key",key);}
}
