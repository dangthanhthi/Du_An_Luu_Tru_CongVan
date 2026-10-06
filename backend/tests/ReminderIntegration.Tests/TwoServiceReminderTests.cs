extern alias doc;
using D=doc::DocumentService;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Data;
using NotificationService.Models;
using Xunit;
namespace ReminderIntegration.Tests;
public sealed class TwoServiceReminderTests
{
    private const string Key="Reminder-integration-test-only-key-not-a-live-secret";
    [Theory][InlineData(false)][InlineData(true)] public async Task Weekly_reminder_to_durable_notification_recovers_crash_or_stops_changed_projection(bool changed)
    {
        await using var notification=new NotificationHost();var clock=new Clock();var directory=new Directory();var lost=new LoseAck(notification.Server.CreateHandler());await using var documents=new DocumentHost(clock,directory,lost,ServiceToken());
        Guid batch;
        using(var s=documents.Services.CreateScope()){
            var db=s.ServiceProvider.GetRequiredService<D.DocumentDbContext>();
            for(var i=0;i<2;i++){var who=directory.People[i];var id=Guid.NewGuid();db.Add(new D.DocumentRegistration{DocumentId=id,Document=new D.Document{Id=id,DocType="INTERNAL",Status="InProgress",Title="Private subject excluded",DocumentNumber=$"26-09-000{i+1}/INT/HL/ADM",CreatedByUserId=who},Kind="INTERNAL",RegistrationDate=new(2026,9,1),RegistrationYear=2026,SequenceNumber=i+1,OwnerDepartmentId=directory.Department,OwnerDepartmentNameSnapshot="ADM",OwnerDepartmentCodeSnapshot="ADM",CompanyCode="HL",CompanyNameSnapshot="Company",InputterUserId=who,OriginatorUserId=who,LastModifierUserId=who,Sensitivity="Normal"});}
            await db.SaveChangesAsync();var service=s.ServiceProvider.GetRequiredService<D.WeeklyReminders>();batch=(await service.PlanAsync(directory.Department)).Id;Assert.Equal("PendingAcceptance",await service.DispatchAsync(batch));
            // Simulate a crash before the final aggregate batch acknowledgement.
            await db.Set<D.ReminderBatch>().Where(x=>x.Id==batch).ExecuteUpdateAsync(u=>u.SetProperty(x=>x.State,"Dispatching").SetProperty(x=>x.LeaseToken,(Guid?)Guid.NewGuid()).SetProperty(x=>x.LeaseUntilUnix,clock.Now.AddMinutes(2).ToUnixTimeSeconds()));
            if(changed)await db.Documents.ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Cancelled"));
        }
        clock.Now=clock.Now.AddMinutes(5);
        using(var s=documents.Services.CreateScope()){Assert.Equal(changed?"RequiresReconciliation":"Queued",await s.ServiceProvider.GetRequiredService<D.WeeklyReminders>().DispatchAsync(batch));var rows=await s.ServiceProvider.GetRequiredService<D.DocumentDbContext>().Set<D.ReminderDelivery>().AsNoTracking().ToArrayAsync();Assert.Equal(2,rows.Length);if(!changed){Assert.All(rows,x=>{Assert.Equal("Accepted",x.State);Assert.Equal("Queued",x.NotificationState);});Assert.Equal(2,rows.Select(x=>x.NotificationId).Distinct().Count());}else Assert.Single(rows.Where(x=>x.State=="Accepted"));}
        using(var s=notification.Services.CreateScope()){var db=s.ServiceProvider.GetRequiredService<NotificationDbContext>();var inbox=await db.Set<DeliveryInbox>().ToArrayAsync();Assert.Equal(2,inbox.Length);Assert.Equal(2,await db.InAppNotifications.CountAsync());Assert.All(inbox,x=>{Assert.Equal("Queued",x.State);Assert.DoesNotContain("Private subject",x.PayloadJson);Assert.Contains("leader@example.test",x.PayloadJson);});}
        Assert.Equal(changed?2:3,lost.Calls);
    }
    private static string ServiceToken()=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:[new("sub","eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),new("das_capability","NotificationSend")],expires:DateTime.UtcNow.AddMinutes(10),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),SecurityAlgorithms.HmacSha256)));
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-05T01:00:00Z");public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Directory:D.IReminderDirectory
    {public Guid Department=Guid.NewGuid(),Actor=Guid.NewGuid();public Guid[] People=[Guid.NewGuid(),Guid.NewGuid()];public Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([Department]);public Task<D.ReminderDirectory> ResolveAsync(Guid d,CancellationToken ct)=>Task.FromResult(new D.ReminderDirectory(Department,new(Actor,true,true,true,false,new HashSet<Guid>{Department},new HashSet<Guid>(),new HashSet<Guid>()),People.Select((x,i)=>new D.ReminderPerson(x,true,$"inputter{i}@example.test")).ToArray(),[new(Guid.NewGuid(),true,"leader@example.test")]));}
    private sealed class LoseAck(HttpMessageHandler inner):DelegatingHandler(inner)
    {public int Calls;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){var n=Interlocked.Increment(ref Calls);var response=await base.SendAsync(request,ct);if(n==1){Assert.Equal(System.Net.HttpStatusCode.Accepted,response.StatusCode);response.Dispose();throw new IOException("Simulated acknowledgement loss after durable commit");}return response;}}
    private sealed class NotificationHost:WebApplicationFactory<NotificationDbContext>
    {private readonly string root=Path.Combine(Path.GetTempPath(),"das-reminder-notification-"+Guid.NewGuid().ToString("N"));protected override void ConfigureWebHost(IWebHostBuilder b){System.IO.Directory.CreateDirectory(root);b.UseEnvironment("Development");b.UseSetting("Database:Initialize","true");b.UseSetting("Jwt:Secret",Key);b.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));}public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();System.IO.Directory.Delete(root,true);}}
    private sealed class DocumentHost(Clock clock,Directory directory,HttpMessageHandler handler,string token):WebApplicationFactory<D.DocumentDbContext>
    {private readonly string root=Path.Combine(Path.GetTempPath(),"das-reminder-document-"+Guid.NewGuid().ToString("N"));protected override void ConfigureWebHost(IWebHostBuilder b){System.IO.Directory.CreateDirectory(root);b.UseEnvironment("Development");b.UseSetting("Database:Provider","Sqlite");b.UseSetting("Database:Initialize","true");b.UseSetting("Jwt:Secret",Key);b.UseSetting("ConnectionStrings:Default","Data Source="+Path.Combine(root,"test.db"));b.UseSetting("Reminders:TransportEnabled","true");b.UseSetting("Notifications:TransportEnabled","true");b.UseSetting("Notifications:Endpoint","http://localhost/api/notifications/send");b.UseSetting("Notifications:ServiceToken",token);b.ConfigureServices(s=>{s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(clock);s.RemoveAll<D.IReminderDirectory>();s.AddSingleton<D.IReminderDirectory>(directory);s.AddHttpClient(nameof(D.IReminderNotificationTransport)).ConfigurePrimaryHttpMessageHandler(()=>handler);});}public override async ValueTask DisposeAsync(){await base.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();System.IO.Directory.Delete(root,true);}}
}
