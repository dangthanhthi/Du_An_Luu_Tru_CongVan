using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace DocumentService.Tests;
public sealed class PartnerMetadataClientTests
{
    [Theory]
    [InlineData(401)][InlineData(403)][InlineData(429)][InlineData(500)][InlineData(503)]
    public async Task Dependency_failure_is_not_unknown_entity(int status)
    {
        using var http=new HttpClient(new Handler(_=>new((HttpStatusCode)status))){BaseAddress=new("http://fixture")};
        await Assert.ThrowsAnyAsync<HttpRequestException>(()=>new PartnerServiceClient(http).GetPartnerByIdAsync(Guid.NewGuid()));
    }
    [Fact]
    public async Task Network_failure_is_not_unknown_entity()
    {
        using var http=new HttpClient(new Handler(_=>throw new HttpRequestException("offline"))){BaseAddress=new("http://fixture")};
        await Assert.ThrowsAnyAsync<HttpRequestException>(()=>new PartnerServiceClient(http).GetPartnerByIdAsync(Guid.NewGuid()));
    }
    [Fact]
    public async Task Only_404_means_unknown_entity()
    {
        using var http=new HttpClient(new Handler(_=>new(HttpStatusCode.NotFound))){BaseAddress=new("http://fixture")};
        Assert.Null(await new PartnerServiceClient(http).GetPartnerByIdAsync(Guid.NewGuid()));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Historical_entity_retains_name_but_deleted_is_not_active(bool deleted)
    {
        var id=Guid.NewGuid();using var http=new HttpClient(new Handler(_=>Reply(new{id,fullName="History",shortName=(string?)null,entityType="Both",email=(string?)null,phone=(string?)null,isActive=!deleted,isDeleted=deleted}))){BaseAddress=new("http://fixture")};
        var result=await new PartnerServiceClient(http).GetPartnerByIdAsync(id);Assert.NotNull(result);Assert.Equal(id,result.Id);Assert.Equal("History",result.FullName);Assert.Equal(!deleted,result.IsActive);
    }
    [Theory]
    [InlineData("identity")][InlineData("missing-active")][InlineData("deleted-active")]
    public async Task Malformed_authoritative_reference_is_dependency_failure(string mode)
    {
        var id=Guid.NewGuid();object data=mode switch{
            "identity"=>new{id=Guid.NewGuid(),fullName="Other",shortName=(string?)null,entityType="Both",isActive=true,isDeleted=false},
            "missing-active"=>new{id,fullName="Other",entityType="Both",isDeleted=false},
            _=>new{id,fullName="Other",shortName=(string?)null,entityType="Both",isActive=true,isDeleted=true}};
        using var http=new HttpClient(new Handler(_=>Reply(data))){BaseAddress=new("http://fixture")};
        await Assert.ThrowsAnyAsync<HttpRequestException>(()=>new PartnerServiceClient(http).GetPartnerByIdAsync(id));
    }
    private static HttpResponseMessage Reply(object data)=>new(HttpStatusCode.OK){Content=JsonContent.Create(new{success=true,data})};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> reply):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(reply(r));}
}
