using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class ReminderAuditTests
{
    [Fact] public async Task Delivery_audit_requires_capability_and_department_scope_and_never_exposes_payload()
    {
        var user=Guid.NewGuid();var department=Guid.NewGuid();await using var parent=new V2HttpTests.Host();await using var host=parent.WithWebHostBuilder(b=>b.ConfigureServices(s=>s.AddScoped<IReminderOperatorAuthority>(_=>new Operator(user,department))));Guid id;
        using(var s=host.Services.CreateScope()){var db=s.ServiceProvider.GetRequiredService<DocumentDbContext>();var batch=new ReminderBatch{DepartmentId=department,Period=new(2026,10,5),State="Queued"};id=batch.Id;db.Add(batch);db.Add(new ReminderFanoutManifest{BatchId=id,PlanHash=new string('a',64)});db.Add(new ReminderDelivery{BatchId=id,InputterUserId=user,State="Accepted",PayloadJson="Secret body person@example.test",NotificationId=Guid.NewGuid(),NotificationState="Queued"});await db.SaveChangesAsync();}
        using var anonymous=host.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/api/v2/reminders/runs/{id}/deliveries")).StatusCode);
        using var c=host.CreateClient();c.DefaultRequestHeaders.Authorization=new("Bearer",Token(user,false));Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/v2/reminders/runs/{id}/deliveries")).StatusCode);c.DefaultRequestHeaders.Authorization=new("Bearer",Token(user,true));
        var response=await c.GetAsync($"/api/v2/reminders/runs/{id}/deliveries");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("no-store",response.Headers.CacheControl?.ToString());var body=await response.Content.ReadAsStringAsync();Assert.Contains("Accepted",body);Assert.DoesNotContain("Secret",body);Assert.DoesNotContain("example.test",body);Assert.DoesNotContain("inputter",body,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"/api/v2/reminders/runs/{id}/deliveries?pageSize=101")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/reminders/runs/{Guid.NewGuid()}/deliveries")).StatusCode);
        Guid foreign;using(var s=host.Services.CreateScope()){var db=s.ServiceProvider.GetRequiredService<DocumentDbContext>();var b=new ReminderBatch{DepartmentId=Guid.NewGuid(),Period=new(2026,10,5)};db.Add(b);await db.SaveChangesAsync();foreign=b.Id;}Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v2/reminders/runs/{foreign}/deliveries")).StatusCode);
        c.DefaultRequestHeaders.Authorization=new("Bearer",Token(Guid.NewGuid(),true));Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync($"/api/v2/reminders/runs/{id}/deliveries")).StatusCode);
    }
    private static string Token(Guid user,bool cap)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:cap?[new("sub",user.ToString()),new("das_capability","ReminderOperate")]:[new("sub",user.ToString()),new("role","Admin")],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(V2HttpTests.Host.Key)),SecurityAlgorithms.HmacSha256)));
    private sealed class Operator(Guid user,Guid department):IReminderOperatorAuthority {public Task<ReminderOperator> ResolveAsync(Guid u,CancellationToken ct)=>Task.FromResult(new ReminderOperator(user,true,new HashSet<Guid>{department}));}
}
