using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerHttpTests
{
    [Theory]
    [InlineData("/api/partners")][InlineData("/api/partners/reference")]
    public async Task Catalog_reads_require_authentication(string path)
    {
        await using var h=new Host();using var c=h.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Short_name_and_tax_code_are_optional()
    {
        await using var h=new Host();using var c=h.Client(true);
        Assert.Equal(HttpStatusCode.Created,(await c.PostAsJsonAsync("/api/partners",new {fullName="Entity",shortName="",entityType="Both"})).StatusCode);
    }
    [Fact]
    public async Task Admin_role_alone_cannot_manage_catalog()
    {
        await using var h=new Host();using var c=h.Client(false);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync("/api/partners",new {fullName="Entity",shortName="RED",entityType="Both"})).StatusCode);
    }
    [Fact]
    public async Task Contacts_update_deactivate_delete_restore_and_stale_write_preserve_audit()
    {
        await using var h=new Host();using var c=h.Client(true,"Secretary");
        var created=await Data(await c.PostAsJsonAsync("/api/partners",new {fullName=" Original ",contactPerson=" Mai ",contactInformation=" Desk 1 "}),HttpStatusCode.Created);
        var id=created.GetProperty("id").GetGuid();Assert.Equal("Mai",created.GetProperty("contactPerson").GetString());Assert.Equal(1,created.GetProperty("version").GetInt64());
        var update=new {fullName="Changed",expectedVersion=1,isActive=false,contactPerson="An",contactInformation="Desk 2"};
        var edited=await Data(await c.PutAsJsonAsync($"/api/partners/{id}",update));Assert.False(edited.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/partners/{id}",update)).StatusCode);
        await Change(c,id,"DELETE",2);var historical=await Data(await c.GetAsync($"/api/partners/{id}"));Assert.True(historical.GetProperty("isDeleted").GetBoolean());Assert.False(historical.GetProperty("isActive").GetBoolean());
        var list=await Data(await c.GetAsync("/api/partners?isActive=true"));Assert.Equal(0,list.GetProperty("totalCount").GetInt32());
        list=await Data(await c.GetAsync("/api/partners?includeDeleted=true"));Assert.Equal(1,list.GetProperty("totalCount").GetInt32());
        var restored=await Data(await c.PostAsJsonAsync($"/api/partners/{id}/restore",new {expectedVersion=3}));Assert.False(restored.GetProperty("isActive").GetBoolean());Assert.False(restored.GetProperty("isDeleted").GetBoolean());Assert.Equal(4,restored.GetProperty("version").GetInt64());
        using var scope=h.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
        var audits=await db.PartnerAudits.OrderBy(x=>x.Version).ToListAsync();Assert.Equal(new[]{"Create","Update","Delete","Restore"},audits.Select(x=>x.Action));Assert.All(audits,x=>Assert.Equal(h.Actor,x.ActorUserId));
        Assert.Equal("Changed",(await db.Partners.SingleAsync()).FullName);
    }
    [Theory]
    [InlineData("shortName")][InlineData("taxCode")]
    public async Task Supplied_codes_are_reserved_case_insensitively_even_after_deletion(string field)
    {
        await using var h=new Host();using var c=h.Client(true);
        var d=new Dictionary<string,object>{{"fullName","Entity"},{field," ab-1 "}};
        var id=(await Data(await c.PostAsJsonAsync("/api/partners",d),HttpStatusCode.Created)).GetProperty("id").GetGuid();await Change(c,id,"DELETE",1);
        d[field]="AB-1";Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/partners",d)).StatusCode);
        using var scope=h.Services.CreateScope();Assert.Equal(2,await scope.ServiceProvider.GetRequiredService<PartnerDbContext>().PartnerAudits.CountAsync());
    }
    [Fact]
    public async Task Multiple_entities_without_optional_codes_are_valid()
    {
        await using var h=new Host();using var c=h.Client(true);
        for(var i=0;i<3;i++)Assert.Equal(HttpStatusCode.Created,(await c.PostAsJsonAsync("/api/partners",new{fullName="Entity "+i})).StatusCode);
        var page=await Data(await c.GetAsync("/api/partners?searchTerm=ENTITY&pageNumber=2&pageSize=2"));Assert.Equal(3,page.GetProperty("totalCount").GetInt32());Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.False(page.GetProperty("items")[0].TryGetProperty("createdByUserId",out _));
    }
    [Theory]
    [InlineData("pageSize=101")][InlineData("pageNumber=2147483647")][InlineData("pageSize=1&pageSize=2")][InlineData("isActive=bad")][InlineData("actorUserId=1")][InlineData("entityType=Company")]
    public async Task Invalid_lookup_is_not_silently_widened(string query)
    {
        await using var h=new Host();using var c=h.Client(false);Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/partners?"+query)).StatusCode);
    }
    [Fact]
    public async Task Missing_subject_unknown_fields_and_invalid_contact_do_not_write()
    {
        await using var h=new Host();using var noSubject=h.Client(true,subject:false);
        Assert.Equal(HttpStatusCode.Forbidden,(await noSubject.PostAsJsonAsync("/api/partners",new{fullName="Test"})).StatusCode);
        using var c=h.Client(true);
        foreach(var data in new object[]{new{fullName="Test",createdByUserId=Guid.NewGuid()},new{fullName=" "},new{fullName="Test",email="bad@"},new{fullName="Test",phone="abcdefgh"},new{fullName="Test",contactPerson=new string('x',501)}})
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/partners",data)).StatusCode);
        using var scope=h.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<PartnerDbContext>().PartnerAudits.ToListAsync());
    }
    [Fact]
    public async Task Capability_is_server_supplied_and_ordinary_readers_cannot_list_deleted_or_mutate()
    {
        await using var h=new Host();using var manager=h.Client(true,"Employee");
        var options=await Data(await manager.GetAsync("/api/partners/options"));Assert.True(options.GetProperty("canManage").GetBoolean());
        var id=(await Data(await manager.PostAsJsonAsync("/api/partners",new{fullName="Entity"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var reader=h.Client(false,"Secretary");options=await Data(await reader.GetAsync("/api/partners/options"));Assert.False(options.GetProperty("canManage").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.GetAsync("/api/partners?includeDeleted=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.PutAsJsonAsync($"/api/partners/{id}",new{fullName="Bad",expectedVersion=1})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Change(reader,id,"DELETE",1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.PostAsJsonAsync($"/api/partners/{id}/restore",new{expectedVersion=1})).StatusCode);
    }
    [Fact]
    public async Task Deleted_active_entry_is_not_selectable_but_restores_previous_active_flag()
    {
        await using var h=new Host();using var c=h.Client(true);
        var id=(await Data(await c.PostAsJsonAsync("/api/partners",new{fullName="History"}),HttpStatusCode.Created)).GetProperty("id").GetGuid();await Change(c,id,"DELETE",1);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/partners/{id}",new{fullName="Hidden",expectedVersion=2})).StatusCode);
        var p=await Data(await c.GetAsync("/api/partners/reference"));Assert.Empty(p.GetProperty("items").EnumerateArray());
        p=await Data(await c.PostAsJsonAsync($"/api/partners/{id}/restore",new{expectedVersion=2}));Assert.True(p.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync($"/api/partners/{id}/restore",new{expectedVersion=0})).StatusCode);
    }
    [Fact]
    public async Task Contact_information_and_address_accept_line_breaks()
    {
        await using var h=new Host();using var c=h.Client(true);
        var p=await Data(await c.PostAsJsonAsync("/api/partners",new{fullName="Multiline",contactInformation="Desk 1\nDesk 2",address="Street\nCity"}),HttpStatusCode.Created);
        Assert.Equal("Desk 1\nDesk 2",p.GetProperty("contactInformation").GetString());
    }
    internal static Task<HttpResponseMessage> Change(HttpClient c,Guid id,string method,long version)=>c.SendAsync(new(new HttpMethod(method),$"/api/partners/{id}"){Content=JsonContent.Create(new{expectedVersion=version})});
    internal static async Task<JsonElement> Data(HttpResponseMessage r,HttpStatusCode status=HttpStatusCode.OK)
    {Assert.Equal(status,r.StatusCode);Assert.Equal("no-store",r.Headers.CacheControl?.ToString());using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return j.RootElement.GetProperty("data").Clone();}
    internal sealed class Host(Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? interceptor=null):WebApplicationFactory<PartnerBusinessService>
    {
        internal const string Key="Partner-http-test-only-key-not-a-live-credential";
        internal readonly Guid Actor=Guid.NewGuid();
        private readonly string root=Path.Combine(Path.GetTempPath(),"das-partner-http-"+Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            Directory.CreateDirectory(root);b.UseEnvironment("Development");b.UseSetting("Database:InitializeOnStartup","true");
            b.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));b.UseSetting("Jwt:Secret",Key);
            if(interceptor is not null)b.ConfigureServices(s=>s.AddDbContext<PartnerDbContext>(o=>o.AddInterceptors(interceptor)));
        }
        internal HttpClient Client(bool manage,string role="Admin",bool subject=true)
        {
            var c=CreateClient();var claims=new List<Claim>{new(ClaimTypes.Role,role)};
            if(subject)claims.Add(new("sub",Actor.ToString()));if(manage)claims.Add(new("das_capability","CatalogManage"));
            var token=new JwtSecurityToken(claims:claims,expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),SecurityAlgorithms.HmacSha256));
            c.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return c;
        }
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
