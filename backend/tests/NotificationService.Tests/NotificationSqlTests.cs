using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using Xunit;
namespace NotificationService.Tests;
public sealed class NotificationSqlTests
{
    [NotificationSqlFact] public async Task Concurrent_acceptance_is_atomic_and_sender_keys_do_not_duplicate_the_inapp_notification()
    {await using var f=await Fixture.Create();var sender=Guid.NewGuid();var message=new DurableMessage(Guid.NewGuid(),"recipient@example.test","Subject","Body",null,"Info",null);
        var results=await Task.WhenAll(Enumerable.Range(0,12).Select(async _=>{await using var db=f.Db();return await new DurableNotifications(db).AcceptAsync(sender,"same-event",message,default);}));Assert.Single(results.Select(x=>x.Id).Distinct());await using var check=f.Db();Assert.Single(await check.Set<DeliveryInbox>().ToListAsync());Assert.Single(await check.InAppNotifications.ToListAsync());}
    [NotificationSqlFact] public async Task Concurrent_delivery_lease_sends_once_and_persists_the_receipt()
    {await using var f=await Fixture.Create();Guid id;await using(var db=f.Db())id=(await new DurableNotifications(db).AcceptAsync(Guid.NewGuid(),"deliver",new(null,"recipient@example.test","Subject","Body",null,"Info",null),default)).Id;
        var email=new Email();var results=await Task.WhenAll(Enumerable.Range(0,12).Select(async _=>{await using var db=f.Db();return await new DurableDelivery(db,email,TimeProvider.System).ProcessAsync(id,default);}));Assert.Equal(1,email.Calls);Assert.Equal(1,results.Count(x=>x=="Sent"));await using var check=f.Db();Assert.Equal("Sent",(await check.Set<DeliveryInbox>().SingleAsync()).State);}
    private sealed class Email:IDeliveryEmail{public int Calls;public async Task<string> SendAsync(DurableMessage m,Guid id,CancellationToken ct){Interlocked.Increment(ref Calls);await Task.Delay(50,ct);return "Sent";}}
    internal sealed class Fixture:IAsyncDisposable
    {
        private readonly string master=Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;private readonly string name="DAS_Notification_QA_"+Guid.NewGuid().ToString("N");
        public NotificationDbContext Db()=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString).Options);
        public static async Task<Fixture> Create(bool migrate=true){var f=new Fixture();try{await using var c=new SqlConnection(f.master);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText="CREATE DATABASE ["+f.name+"]";await cmd.ExecuteNonQueryAsync();if(migrate){await using var db=f.Db();await db.Database.MigrateAsync();}return f;}catch{await f.DisposeAsync();throw;}}
        public async ValueTask DisposeAsync(){SqlConnection.ClearAllPools();await using var c=new SqlConnection(master);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText="IF DB_ID('"+name+"') IS NOT NULL BEGIN ALTER DATABASE ["+name+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+name+"]; END";await cmd.ExecuteNonQueryAsync();}
    }
}
public sealed class NotificationSqlFactAttribute:FactAttribute
{public NotificationSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))Skip="Requires isolated SQL QA script.";}}
