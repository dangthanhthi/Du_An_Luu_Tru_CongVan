using System.Net;
using Xunit;
namespace DocumentService.Tests;

public sealed class SubsequentHttpTests
{
    [Theory]
    [InlineData("/api/v2/reports/incomplete")]
    [InlineData("/api/v2/reports/incomplete/export")]
    [InlineData("/api/v2/my-staff")]
    public async Task Missing_trusted_upstream_is_unavailable_not_demo(string route)
    {
        await using var host=new V2HttpTests.Host();using var client=host.Client(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(route)).StatusCode);
    }
    [Theory]
    [InlineData("/api/v2/reports/incomplete")]
    [InlineData("/api/v2/my-staff")]
    public async Task Anonymous_access_is_rejected(string route)
    {
        await using var host=new V2HttpTests.Host();using var client=host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(route)).StatusCode);
    }
    [Fact] public async Task Generic_admin_cannot_operate_weekly_reminders()
    {await using var host=new V2HttpTests.Host();using var client=host.Client(Guid.NewGuid());Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/v2/reminders/runs")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync("/api/v2/reminders/departments/"+Guid.NewGuid()+"/run",null)).StatusCode);}
}
