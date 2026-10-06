using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Data;
using Xunit;
namespace NotificationService.Tests;
public sealed class NotificationHttpTests
{
    [Theory][InlineData("/api/notifications/my")][InlineData("/api/notifications/logs")][InlineData("/api/notifications/preferences")]
    public async Task Anonymous_cannot_read(string route)
    {await using var host=new Host();using var c=host.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync(route)).StatusCode);}
    [Fact] public async Task Generic_admin_cannot_send_or_read_audit()
    {await using var host=new Host();using var c=host.Client(Guid.NewGuid());Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync("/api/notifications/send",new{recipientUserId=Guid.NewGuid(),subject="Test",body="Test"})).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/notifications/logs")).StatusCode);}
    internal sealed class Host:WebApplicationFactory<NotificationDbContext>
    {
        internal const string Key="Notification-test-only-key-not-a-live-credential";private readonly string root=Path.Combine(Path.GetTempPath(),"das-notification-"+Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder b){Directory.CreateDirectory(root);b.UseEnvironment("Development");b.UseSetting("Database:Initialize","true");b.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));b.UseSetting("Jwt:Secret",Key);}
        internal HttpClient Client(Guid user,string? capability=null){var c=CreateClient();var claims=new List<Claim>{new("sub",user.ToString()),new(ClaimTypes.Role,"Admin")};if(capability is not null)claims.Add(new("das_capability",capability));var token=new JwtSecurityToken(claims:claims,expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),SecurityAlgorithms.HmacSha256));c.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return c;}
        public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
