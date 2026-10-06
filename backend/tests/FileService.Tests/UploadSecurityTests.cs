using System.Security.Claims;
using System.Text;
using FilesService.Controllers;
using FilesService.Data;
using FilesService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FileService.Tests;

public sealed class UploadSecurityTests
{
    [Fact]
    public void All_file_routes_require_authentication_without_anonymous_override()
    {
        Assert.NotNull(typeof(FilesController).GetCustomAttributes(typeof(AuthorizeAttribute),true).SingleOrDefault());
        Assert.Empty(typeof(FilesController).GetMethods().SelectMany(x=>x.GetCustomAttributes(typeof(AllowAnonymousAttribute),true)));
    }
    [Fact]
    public async Task Missing_principal_does_not_become_a_fallback_uploader()
    {
        await using var f=await Fixture.Create(); var c=new FilesController(f.Service()) { ControllerContext=new(){HttpContext=new DefaultHttpContext()} };
        var result=await c.Upload(Form(Encoding.ASCII.GetBytes("%PDF-not-a-real-pdf")));
        Assert.Equal(401,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode); Assert.Empty(await f.Db.Files.ToListAsync());
    }
    [Theory]
    [InlineData("noIdentity")][InlineData("text")][InlineData("corrupt")][InlineData("extension")]
    public async Task Invalid_uploads_never_become_file_metadata(string invalid)
    {
        await using var f=await Fixture.Create(); var bytes=Encoding.ASCII.GetBytes(invalid=="corrupt"?"%PDF-1.7\ncorrupt\n%%EOF":"Not a PDF");
        var e=await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().UploadFileAsync(Form(bytes,invalid=="extension"?"document.exe":"document.pdf"),invalid=="noIdentity"?Guid.Empty:Guid.NewGuid()));
        Assert.Contains(e.Status,new[]{401,415,422}); Assert.Empty(await f.Db.Files.ToListAsync());
        Assert.Empty(Directory.GetFiles(f.Root));
    }
    [Fact]
    public async Task Actual_stream_bytes_enforce_limit_even_when_client_length_is_small()
    {
        await using var f=await Fixture.Create();var file=new UnderreportedFile(new byte[2048]);
        var e=await Assert.ThrowsAsync<FileRuleException>(()=>f.Service(1024).UploadFileAsync(file,Guid.NewGuid()));
        Assert.Equal(413,e.Status); Assert.Empty(await f.Db.Files.ToListAsync());
    }
    [Theory]
    [InlineData("missing")][InlineData("relative")][InlineData("absentDirectory")]
    public async Task Storage_must_be_explicit_and_available_without_fallback(string invalid)
    {
        await using var f=await Fixture.Create();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Storage:Path"]=invalid switch { "missing"=>null,"relative"=>"relative-uploads",_=>Path.Combine(f.Root,"not-created") } }).Build();
        Assert.Throws<InvalidOperationException>(()=>new FileStorageService(f.Db,config));
    }
    internal static IFormFile Form(byte[] bytes,string name="document.pdf")=>new FormFile(new MemoryStream(bytes),0,bytes.Length,"file",name){Headers=new HeaderDictionary(),ContentType="application/pdf"};
    [Theory]
    [InlineData("controls")][InlineData("long")][InlineData("noExtension")]
    public async Task Invalid_display_names_are_rejected_before_writing_a_durable_intent(string kind)
    {
        await using var f=await Fixture.Create();var name=kind switch{"controls"=>"bad\r\n.pdf","long"=>new string('x',201)+".pdf",_=>"no-extension"};
        Assert.Equal(415,(await Assert.ThrowsAsync<FileRuleException>(()=>f.Service().UploadFileAsync(Form(PdfStorageTests.Pdf(),name),Guid.NewGuid()))).Status);
        Assert.Empty(await f.Db.Set<FilesService.Models.Entities.PdfUpload>().ToListAsync());Assert.Empty(Directory.GetFiles(f.Root));
    }
    [Theory]
    [InlineData("0")][InlineData("26214401")][InlineData("not-a-size")]
    public async Task Invalid_operational_byte_limit_never_enables_unlimited_upload(string maximum)
    {
        await using var f=await Fixture.Create();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=f.Root,["Storage:MaxBytes"]=maximum}).Build();
        Assert.Throws<InvalidOperationException>(()=>new FileStorageService(f.Db,config));
    }
    private sealed class UnderreportedFile(byte[] bytes):IFormFile {
        public string ContentType=>"application/pdf";public string ContentDisposition=>"";public IHeaderDictionary Headers=>new HeaderDictionary();
        public long Length=>1; public string Name=>"file";public string FileName=>"document.pdf";
        public Stream OpenReadStream()=>new MemoryStream(bytes);public void CopyTo(Stream target)=>OpenReadStream().CopyTo(target);
        public Task CopyToAsync(Stream target,CancellationToken ct=default)=>OpenReadStream().CopyToAsync(target,ct); }
    internal sealed class Fixture:IAsyncDisposable
    {
        public string Root=Path.Combine(Path.GetTempPath(),"das-file-qa-"+Guid.NewGuid().ToString("N"));
        private readonly SqliteConnection connection=new("Data Source=:memory:"); public FileDbContext Db=null!;
        public static async Task<Fixture> Create(IInterceptor? interceptor=null,bool retry=false){var f=new Fixture();Directory.CreateDirectory(f.Root);await f.connection.OpenAsync();var b=new DbContextOptionsBuilder<FileDbContext>();if(retry)b.UseSqlite(f.connection,s=>s.ExecutionStrategy(d=>new RetryOnce(d)));else b.UseSqlite(f.connection);if(interceptor is not null)b.AddInterceptors(interceptor);f.Db=new(b.Options);await f.Db.Database.EnsureCreatedAsync();return f;}
        private sealed class RetryOnce(ExecutionStrategyDependencies d):ExecutionStrategy(d,1,TimeSpan.Zero){protected override bool ShouldRetryOn(Exception e)=>e is TimeoutException;}
        public FileStorageService Service(long max=25*1024*1024,IPdfThreatScanner? scanner=null)=>new(Db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Path"]=Root,["Storage:MaxBytes"]=max.ToString()}).Build(),scanner);
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await connection.DisposeAsync();Directory.Delete(Root,true);}
    }
}
