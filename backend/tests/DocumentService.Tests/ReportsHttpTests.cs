using System.Net;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReportsHttpTests
{
    [Fact] public async Task Report_only_authority_cannot_open_documents_or_export_without_export_grant()
    {var authority=new Authority();await using var h=new V2HttpTests.Host(reportAuthority:authority);using var c=h.Client(authority.User);Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/api/v2/reports/incomplete")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/v2/reports/incomplete/export")).StatusCode);Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync("/api/v2/documents?kind=Internal")).StatusCode);}
    [Theory][InlineData("?kind=Incoming")][InlineData("?includeRecent=true&includeRecent=false")][InlineData("?userId=11111111-1111-4111-8111-111111111111")][InlineData("?pageNumber=0")]
    public async Task Invalid_report_filters_and_caller_scope_are_rejected(string query)
    {var a=new Authority();await using var h=new V2HttpTests.Host(reportAuthority:a);using var c=h.Client(a.User);Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/v2/reports/incomplete"+query)).StatusCode);}
    [Fact] public async Task Export_checks_the_distinct_grant_and_returns_a_real_zip_workbook()
    {var a=new Authority{Export=true};await using var h=new V2HttpTests.Host(reportAuthority:a);using var c=h.Client(a.User);var response=await c.GetAsync("/api/v2/reports/incomplete/export");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("no-store",response.Headers.CacheControl?.ToString());Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",response.Content.Headers.ContentType?.MediaType);Assert.Equal(new byte[]{80,75,3,4},(await response.Content.ReadAsByteArrayAsync()).Take(4));}
    private sealed class Authority:IReportAuthority
    {public Guid User=Guid.NewGuid();public bool Export;public Task<ReportAuthority> ResolveAsync(Guid user,CancellationToken ct)=>Task.FromResult(new ReportAuthority(User,true,true,Export,false,new HashSet<Guid>(),new HashSet<Guid>(),new HashSet<Guid>()));}
}
