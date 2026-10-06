using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

public static class WeeklyReminderSchedule
{
    public const string TimeZone="Asia/Ho_Chi_Minh";
    public static DateOnly DuePeriod(DateTimeOffset now)
    {
        var local=now.ToOffset(TimeSpan.FromHours(7));var date=DateOnly.FromDateTime(local.DateTime);
        var monday=date.AddDays(-(((int)local.DayOfWeek+6)%7));
        return date==monday&&local.TimeOfDay<TimeSpan.FromHours(8)?monday.AddDays(-7):monday;
    }
    public static DateTimeOffset ScheduledAt(DateOnly period)=>new(period.ToDateTime(new TimeOnly(8,0)),TimeSpan.FromHours(7));
}
public sealed class ReminderBatch
{
    public Guid Id {get;set;}=Guid.NewGuid();public Guid DepartmentId {get;set;}public DateOnly Period {get;set;}
    public string State {get;set;}="Planned";public string PayloadJson {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}public long LeaseUntilUnix {get;set;}public Guid? LeaseToken {get;set;}
    public int Attempts {get;set;}public long Version {get;set;}=1;public string? ErrorCode {get;set;}
}
public sealed record ReminderPerson(Guid UserId,bool IsActive,string? Email);
public sealed record ReminderDirectory(Guid DepartmentId,ReportAuthority Scope,IReadOnlyList<ReminderPerson> People,IReadOnlyList<ReminderPerson> Leaders);
public interface IReminderDirectory
{
    Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct);
    Task<ReminderDirectory> ResolveAsync(Guid department,CancellationToken ct);
}
public sealed class UnavailableReminderDirectory:IReminderDirectory
{
    public Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct)=>throw Missing();
    public Task<ReminderDirectory> ResolveAsync(Guid department,CancellationToken ct)=>throw Missing();
    private static DocumentRegistrationRuleException Missing()=>new(503,"REMINDER_DIRECTORY_UNAVAILABLE","Reminder directory is not connected.");
}
public sealed record ReminderEnvelope(Guid InputterUserId,string To,IReadOnlyList<string> Cc,IReadOnlyList<IncompleteRow> Documents);
public sealed record ReminderPlan(DateTimeOffset EvaluatedAt,IReadOnlyList<ReminderEnvelope> Envelopes,IReadOnlyList<string> Warnings);
// A transport must durably accept this idempotency key before acknowledging Queued.
// SMTP completion is a separate receipt and must never be inferred from acceptance.
public interface IReminderTransport {Task<string> EnqueueAsync(Guid key,ReminderPlan plan,CancellationToken ct);}
public sealed class UnavailableReminderTransport:IReminderTransport
{public Task<string> EnqueueAsync(Guid key,ReminderPlan plan,CancellationToken ct)=>Task.FromResult("PendingConfiguration");}

public sealed class WeeklyReminders(DocumentDbContext db,IncompleteReports reports,IReminderDirectory directory,IReminderTransport transport,TimeProvider clock)
{
    public async Task<ReminderBatch> PlanAsync(Guid department,CancellationToken ct=default)
    {
        if(department==Guid.Empty)throw new ArgumentException("Department is required.");
        var period=WeeklyReminderSchedule.DuePeriod(clock.GetUtcNow());
        var existing=await db.Set<ReminderBatch>().AsNoTracking().SingleOrDefaultAsync(x=>x.DepartmentId==department&&x.Period==period,ct);
        if(existing is not null)return existing;
        var plan=await FreshPlan(department,ct);
        var batch=new ReminderBatch{DepartmentId=department,Period=period,CreatedAt=clock.GetUtcNow(),PayloadJson=JsonSerializer.Serialize(plan),State=plan.Envelopes.Count==0?"NoRecipients":"Planned"};
        db.Add(batch);
        try {await db.SaveChangesAsync(ct);}
        catch(DbUpdateException) {db.Entry(batch).State=EntityState.Detached;var winner=await db.Set<ReminderBatch>().AsNoTracking().SingleOrDefaultAsync(x=>x.DepartmentId==department&&x.Period==period,ct);if(winner is null)throw;return winner;}
        return batch;
    }
    public async Task<string> DispatchAsync(Guid id,CancellationToken ct=default)
    {
        var now=clock.GetUtcNow();var token=Guid.NewGuid();var lease=now.AddMinutes(2).ToUnixTimeSeconds();
        var durable=await db.Set<ReminderFanoutManifest>().AnyAsync(x=>x.BatchId==id,ct);
        await db.Set<ReminderBatch>().Where(x=>x.Id==id&&x.State=="Dispatching"&&x.LeaseUntilUnix<=now.ToUnixTimeSeconds()).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,durable?"PendingAcceptance":"UnknownOutcome"),ct);
        var claimed=await db.Set<ReminderBatch>().Where(x=>x.Id==id&&(x.State=="Planned"||x.State=="PendingConfiguration"||x.State=="PendingAcceptance")&&x.LeaseUntilUnix<=now.ToUnixTimeSeconds())
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,lease).SetProperty(x=>x.Attempts,x=>x.Attempts+1).SetProperty(x=>x.Version,x=>x.Version+1),ct);
        if(claimed!=1)return "NotClaimed";
        var batch=await db.Set<ReminderBatch>().AsNoTracking().SingleAsync(x=>x.Id==id,ct);
        string state;ReminderPlan? plan=null;
        try {
            // Never send an old week's snapshot after a long outage.
            if(batch.Period!=WeeklyReminderSchedule.DuePeriod(now))state="Superseded";
            else {
                plan=await FreshPlan(batch.DepartmentId,ct);
                if(plan.Envelopes.Count==0)state=durable?"RequiresReconciliation":"NoRecipients";
                else {
                    // Freeze the verified plan before crossing the external boundary.
                    var saved=await db.Set<ReminderBatch>().Where(x=>x.Id==id&&x.LeaseToken==token&&x.LeaseUntilUnix>clock.GetUtcNow().ToUnixTimeSeconds()).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.PayloadJson,JsonSerializer.Serialize(plan)).SetProperty(x=>x.State,"Dispatching"),ct);
                    if(saved!=1)return "LeaseLost";
                    state=await transport.EnqueueAsync(id,plan,ct);
                    if(state is not ("Queued" or "PendingConfiguration" or "PendingAcceptance" or "RequiresReconciliation" or "DeadLetter"))state="UnknownOutcome";
                }
            }
        }
        catch(DocumentRegistrationRuleException e) when(e.Code=="REMINDER_PLAN_INVALID"){state="RequiresReconciliation";}
        catch(Exception e) when(e is OperationCanceledException or DocumentRegistrationRuleException or HttpRequestException or IOException){state=plan is null?"PendingConfiguration":await db.Set<ReminderFanoutManifest>().AnyAsync(x=>x.BatchId==id,CancellationToken.None)?"PendingAcceptance":"UnknownOutcome";}
        await db.Set<ReminderBatch>().Where(x=>x.Id==id&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUnix,0L).SetProperty(x=>x.Version,x=>x.Version+1),CancellationToken.None);
        return state;
    }
    private async Task<ReminderPlan> FreshPlan(Guid department,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(30));ct=deadline.Token;
        var context=await directory.ResolveAsync(department,ct);var scope=context.Scope;
        if(context.DepartmentId!=department||scope.UserId==Guid.Empty||scope.OrganizationWide||!scope.DepartmentIds.SetEquals(new HashSet<Guid>{department})||scope.ManagedStaffIds.Count!=0||scope.ConfidentialDocumentIds.Count!=0)
            throw new DocumentRegistrationRuleException(503,"REMINDER_AUTHORITY_MISMATCH","Reminder audience is inconsistent.");
        var report=await reports.QueryAsync(scope.UserId,scope,new(DepartmentId:department),1,100,true,ct);
        var warnings=new List<string>();var envelopes=new List<ReminderEnvelope>();var ids=report.Items.Select(x=>x.DocumentId).ToArray();
        var inputters=await db.DocumentRegistrations.AsNoTracking().Where(x=>ids.Contains(x.DocumentId)).ToDictionaryAsync(x=>x.DocumentId,x=>x.InputterUserId,ct);
        if(context.People.GroupBy(x=>x.UserId).Any(x=>x.Count()!=1))throw new DocumentRegistrationRuleException(503,"REMINDER_DIRECTORY_INCONSISTENT","Duplicate directory identities.");
        var leaders=context.Leaders.Where(x=>x.IsActive).Select(x=>Email(x.Email)).Where(x=>x is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(leaders.Length==0)warnings.Add("NO_ACTIVE_LEADERSHIP_EMAIL");
        foreach(var group in report.Items.GroupBy(x=>inputters[x.DocumentId]))
        {
            var person=context.People.SingleOrDefault(x=>x.UserId==group.Key&&x.IsActive);var to=Email(person?.Email);
            if(to is null){warnings.Add("MISSING_INPUTTER_EMAIL:"+group.Key);continue;}
            envelopes.Add(new(group.Key,to,leaders.Where(x=>!x.Equals(to,StringComparison.OrdinalIgnoreCase)).ToArray(),group.ToArray()));
        }
        var confidentialMissing=await db.DocumentRegistrations.AnyAsync(x=>x.OwnerDepartmentId==department&&x.Sensitivity!="Normal"&&(x.Kind=="OUTGOING"||x.Kind=="INTERNAL")&&x.Document!=null&&x.Document.Status!="Cancelled"&&x.RegistrationDate<DocumentNumberFormatter.RegistrationDate(report.EvaluatedAt).AddDays(-ReminderEligibility.AgeThresholdDays),ct);
        if(confidentialMissing)warnings.Add("CONFIDENTIAL_AUDIENCE_POLICY_PENDING");
        return new(report.EvaluatedAt,envelopes,warnings);
    }
    private static string? Email(string? value)=>!string.IsNullOrWhiteSpace(value)&&MailAddress.TryCreate(value.Trim(),out var parsed)&&parsed.Address.Equals(value.Trim(),StringComparison.OrdinalIgnoreCase)?parsed.Address:null;
}

public sealed class WeeklyReminderWorker(IServiceScopeFactory scopes,ILogger<WeeklyReminderWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try {using var scope=scopes.CreateScope();var directory=scope.ServiceProvider.GetRequiredService<IReminderDirectory>();var service=scope.ServiceProvider.GetRequiredService<WeeklyReminders>();
                foreach(var id in await directory.DepartmentsAsync(ct)){var batch=await service.PlanAsync(id,ct);await service.DispatchAsync(batch.Id,ct);}}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){log.LogWarning("Reminder cycle unavailable: {Type}",e.GetType().Name);}
            await Task.Delay(TimeSpan.FromMinutes(5),ct);
        }
    }
}
