using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2HttpTests
{
    [Theory]
    [InlineData("/api/v2/documents?kind=Internal&view=all")]
    [InlineData("/api/v2/documents/options?kind=Internal")]
    public async Task Default_authority_is_unavailable_even_for_signed_admin(string path)
    {
        await using var host=new Host();using var client=host.Client(Guid.NewGuid());
        var response=await client.GetAsync(path);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
    }
    [Fact]
    public async Task Default_register_does_not_accept_browser_admin_as_authority()
    {
        await using var host=new Host();using var client=host.Client(Guid.NewGuid());client.DefaultRequestHeaders.Add("Idempotency-Key","http-registration");
        var response=await client.PostAsJsonAsync("/api/v2/documents",new {kind="Internal",companyCode="HL",subject="Test",originatorUserId=Guid.NewGuid(),ownerDepartmentId=Guid.NewGuid(),sensitivity="Normal"});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
    }
    [Theory]
    [InlineData("Incoming")][InlineData("Outgoing")][InlineData("Internal")]
    public async Task Http_register_replay_edit_preserves_sequence_and_cancel_restore_previous_status(string kind)
    {
        var a=new Authority();await using var host=new Host(a);using var client=host.Client(a.User);
        var draft=Draft(a,kind);var first=await Register(client,draft,"same-key");var id=first.GetProperty("id").GetGuid();
        var replay=await Register(client,draft,"same-key");Assert.Equal(id,replay.GetProperty("id").GetGuid());a.Readable.Add(id);
        var number=first.GetProperty("registrationNumber").GetString();Assert.Contains("0001",number);
        var edit=new{expectedVersion=1,companyCode="HV",subject="Edited",originatorUserId=a.User,ownerDepartmentId=a.OtherDepartment,sensitivity="Confidential",issuedDate="2020-01-01"};
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/v2/documents/{id}",edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/v2/documents/{id}",edit)).StatusCode);
        foreach(var step in new[]{(2,"Distribute",(string?)null),(3,"Cancel","Reason"),(4,"Restore",(string?)null)}){
            var response=await client.PostAsJsonAsync($"/api/v2/documents/{id}/status",new{expectedVersion=step.Item1,action=step.Item2,reason=step.Item3});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
        var detail=await Data(await client.GetAsync($"/api/v2/documents/{id}"));var h=detail.GetProperty("header");Assert.Equal("Distributed",h.GetProperty("status").GetString());Assert.Equal(5,h.GetProperty("version").GetInt64());
        Assert.Equal(number!.Replace("/HL","/HV").Replace("/ADM","/IT"),h.GetProperty("registrationNumber").GetString());Assert.False(h.GetProperty("isComplete").GetBoolean());
        using var scope=host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DocumentDbContext>();Assert.Single(await db.DocumentNumberCounters.ToListAsync());Assert.Equal(1,(await db.DocumentNumberCounters.SingleAsync()).CurrentValue);Assert.Equal(4,await db.Set<DocumentEditAudit>().CountAsync());Assert.Single(await db.RegistrationRequests.ToListAsync());
        var header=await db.DocumentRegistrations.SingleAsync();Assert.Equal(1,header.SequenceNumber);Assert.Equal(a.User,header.LastModifierUserId);Assert.Equal(new DateOnly(2020,1,1),header.IssuedDate);
    }
    [Fact]
    public async Task Scope_filters_before_count_page_search_and_detail_even_for_admin()
    {
        var a=new Authority();await using var host=new Host(a);using var client=host.Client(a.User);
        var first=await Register(client,Draft(a,"Internal"),"first");var id=first.GetProperty("id").GetGuid();await Register(client,Draft(a,"Internal"),"hidden");a.Readable.Add(id);
        var page=await Data(await client.GetAsync("/api/v2/documents?kind=Internal&view=all&pageSize=1"));Assert.Equal(1,page.GetProperty("totalCount").GetInt32());Assert.Single(page.GetProperty("items").EnumerateArray());
        a.Readable.Clear();page=await Data(await client.GetAsync("/api/v2/documents?kind=Internal&view=all"));Assert.Equal(0,page.GetProperty("totalCount").GetInt32());Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v2/documents/{id}")).StatusCode);
        a.Offline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/api/v2/documents?kind=Internal&view=all")).StatusCode);
    }
    [Theory]
    [InlineData("kind=Internal&kind=Outgoing")][InlineData("kind=Internal&pageSize=101")]
    [InlineData("kind=Internal&pageNumber=2147483647")][InlineData("kind=Internal&actorUserId=1")]
    [InlineData("kind=Other")][InlineData("kind=Internal&view=cancelled&status=InProgress")]
    public async Task Bad_or_duplicate_queries_are_rejected(string query)
    {
        await using var host=new Host(new Authority());using var c=host.Client(Guid.NewGuid());Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/v2/documents?"+query)).StatusCode);
    }
    [Theory]
    [InlineData("actor")][InlineData("inactive")][InlineData("department")][InlineData("incoming")]
    public async Task Untrusted_or_inactive_or_foreign_registration_cannot_allocate(string mode)
    {
        var a=new Authority {Mismatch=mode=="actor",Active=mode!="inactive",Foreign=mode=="department",IncomingAllowed=mode!="incoming"};await using var h=new Host(a);using var c=h.Client(a.User);
        c.DefaultRequestHeaders.Add("Idempotency-Key","denied");var r=await c.PostAsJsonAsync("/api/v2/documents",Draft(a,mode=="incoming"?"Incoming":"Internal"));Assert.Equal(mode=="actor"?HttpStatusCode.ServiceUnavailable:HttpStatusCode.Forbidden,r.StatusCode);
        using var scope=h.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<DocumentDbContext>().Documents.ToListAsync());
    }
    [Fact]
    public async Task Unknown_authority_and_number_fields_and_missing_key_are_rejected()
    {
        var a=new Authority();await using var h=new Host(a);using var c=h.Client(a.User);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v2/documents",Draft(a,"Internal"))).StatusCode);
        c.DefaultRequestHeaders.Add("Idempotency-Key","strict");var r=await c.PostAsJsonAsync("/api/v2/documents",new{kind="Internal",companyCode="HL",subject="Test",originatorUserId=a.User,ownerDepartmentId=a.Department,registrationNumber="99999",actor=new{isActive=true}});Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key",new[]{"a","b"});Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v2/documents",Draft(a,"Internal"))).StatusCode);
    }
    [Fact]
    public async Task Relations_are_filtered_and_editor_permission_does_not_grant_read()
    {
        var a=new Authority();await using var h=new Host(a);using var c=h.Client(a.User);var incoming=(await Register(c,Draft(a,"Incoming"),"in")).GetProperty("id").GetGuid();a.Readable.Add(incoming);
        var draft=Draft(a,"Outgoing") with{RelatedDocumentIds=[incoming]};var outgoing=(await Register(c,draft,"out")).GetProperty("id").GetGuid();a.Readable.Add(outgoing);
        var detail=await Data(await c.GetAsync($"/api/v2/documents/{outgoing}"));Assert.Single(detail.GetProperty("relatedDocumentIds").EnumerateArray());a.Readable.Remove(incoming);
        detail=await Data(await c.GetAsync($"/api/v2/documents/{outgoing}"));Assert.Empty(detail.GetProperty("relatedDocumentIds").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{incoming}")).StatusCode);
    }
    [Fact]
    public async Task Anonymous_requests_cannot_read_or_register()
    {
        await using var h=new Host();using var c=h.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/v2/documents?kind=Internal")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/v2/documents",new{})).StatusCode);
    }
    [Fact]
    public async Task Options_come_from_active_server_authority_and_catalogs()
    {
        var a=new Authority();await using var h=new Host(a);using var c=h.Client(a.User);var data=await Data(await c.GetAsync("/api/v2/documents/options?kind=Internal"));Assert.True(data.GetProperty("canRegister").GetBoolean());Assert.Equal(a.Department,data.GetProperty("targets")[0].GetProperty("departmentId").GetGuid());Assert.NotEmpty(data.GetProperty("catalogs").EnumerateArray());
    }
    [Fact]
    public async Task Changed_body_key_conflicts_and_nested_authority_fields_cannot_be_submitted()
    {
        var a=new Authority();await using var h=new Host(a);using var c=h.Client(a.User);var draft=Draft(a,"Internal");await Register(c,draft,"fixed");
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/v2/documents",draft with{Subject="Other"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v2/documents",new{kind="Internal",companyCode="HL",subject="Test",originatorUserId=a.User,ownerDepartmentId=a.Department,details=new{isActive=true}})).StatusCode);
        using var scope=h.Services.CreateScope();Assert.Equal(1,(await scope.ServiceProvider.GetRequiredService<DocumentDbContext>().DocumentNumberCounters.SingleAsync()).CurrentValue);
    }
    [Fact]
    public async Task Editor_without_read_permission_can_edit_but_cannot_get_document()
    {
        var a=new Authority();await using var h=new Host(a);using var c=h.Client(a.User);var id=(await Register(c,Draft(a,"Internal"),"edit-only")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/v2/documents/{id}",new{expectedVersion=1,companyCode="HL",subject="Edit only",originatorUserId=a.User,ownerDepartmentId=a.Department,sensitivity="Normal"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/documents/{id}")).StatusCode);
    }
    internal static RegisterDocumentRequest Draft(Authority a,string kind)=>new(kind,"HL","HTTP subject",a.User,a.Department,"Normal",new(2020,1,1),Details:kind switch{
        "Incoming"=>new(new(2026,10,5),a.External,MethodCode:"EMAIL",DistributionTargetIds:[BusinessCatalogSeed.Targets()[0].Id]),
        "Outgoing"=>new(RecipientPartnerIds:[a.External]),_=>new()});
    internal static async Task<JsonElement> Register(HttpClient c,RegisterDocumentRequest d,string key){c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.Add("Idempotency-Key",key);return await Data(await c.PostAsJsonAsync("/api/v2/documents",d));}
    internal static async Task<JsonElement> Data(HttpResponseMessage r){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal("no-store",r.Headers.CacheControl?.ToString());using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return j.RootElement.GetProperty("data").Clone();}
    internal sealed class Authority:IDocumentV2Authority
    {
        public readonly Guid User=Guid.NewGuid(),Department=Guid.NewGuid(),OtherDepartment=Guid.NewGuid(),External=Guid.NewGuid();public readonly HashSet<Guid> Readable=[];
        public bool Offline,Mismatch,Foreign;public bool Active=true,IncomingAllowed=true;
        private V2EditorActor Actor(Guid user){if(Offline)throw new DocumentRegistrationRuleException(503,"OFFLINE","offline");return new(Mismatch?Guid.NewGuid():user,Active,Foreign?new HashSet<Guid>():new HashSet<Guid>{Department,OtherDepartment},new HashSet<Guid>(),new HashSet<Guid>());}
        public Task<V2RegistrationAuthority> RegisterAsync(Guid user,V2RegistrationDraft d,CancellationToken ct){if(d.Kind=="INCOMING"&&!IncomingAllowed)throw new DocumentRegistrationRuleException(403,"REGISTER_FORBIDDEN","denied");return Task.FromResult(new V2RegistrationAuthority(Actor(user),new(user,d.OriginatorUserId,d.OwnerDepartmentId,"ADM","Administration"),Refs(),new(user,Readable)));}
        public Task<V2MutationAuthority> MutateAsync(Guid u,Guid d,V2EditDraft? e,CancellationToken ct)=>Task.FromResult(new V2MutationAuthority(Actor(u),e is null?null:new(e.OriginatorUserId,e.OwnerDepartmentId,"IT","IT",true,true),Refs(),new(u,Readable)));
        public Task<V2ReadAuthority> ReadAsync(Guid u,CancellationToken ct)=>Task.FromResult(new V2ReadAuthority(Actor(u),Readable.ToHashSet()));
        public Task<V2FormAuthority> FormAsync(Guid u,string k,CancellationToken ct)=>Task.FromResult(new V2FormAuthority(u,Active,true,[new(User,"Test user",Department,"ADM","Administration",true)]));
        private V2ReferenceSet Refs()=>new(new Dictionary<Guid,ExternalEntityReference>{{External,new(External,"Test external",true)}});
    }
    internal sealed class Host(IDocumentV2Authority? authority=null,IReportAuthority? reportAuthority=null,IStaffAuthority? staffAuthority=null,ITmsConnector? tms=null):WebApplicationFactory<Program>
    {
        internal const string Key="Http-boundary-test-only-key-not-a-live-credential";
        private readonly string root=Path.Combine(Path.GetTempPath(),"das-v2-http-"+Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder){Directory.CreateDirectory(root);builder.UseEnvironment("Development");builder.UseSetting("Database:Provider","Sqlite");builder.UseSetting("Database:Initialize","true");builder.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));builder.UseSetting("Jwt:Secret",Key);builder.ConfigureServices(s=>{if(authority is not null){s.RemoveAll<IDocumentV2Authority>();s.AddSingleton(authority);}if(reportAuthority is not null){s.RemoveAll<IReportAuthority>();s.AddSingleton(reportAuthority);}if(staffAuthority is not null){s.RemoveAll<IStaffAuthority>();s.AddSingleton(staffAuthority);}if(tms is not null){s.RemoveAll<ITmsConnector>();s.AddSingleton(tms);}});}
        internal HttpClient Client(Guid user){var client=CreateClient();var token=new JwtSecurityToken(claims:[new("sub",user.ToString()),new(ClaimTypes.Role,"Admin")],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),SecurityAlgorithms.HmacSha256));client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;}
        public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
