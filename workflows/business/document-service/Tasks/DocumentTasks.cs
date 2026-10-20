using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;
public sealed record DocumentTaskDraft(Guid AssigneeUserId,string Title);
public sealed record DocumentTaskReceipt(Guid CorrelationId,string State,string? TaskId);
public sealed record TaskAssignee(Guid UserId,string Name,string? DepartmentName,bool IsSelf);
public sealed record DocumentTaskOptions(Guid DocumentId,Guid UserId,IReadOnlyList<TaskAssignee> Assignees);
public sealed record DocumentTaskItem(Guid CorrelationId,Guid AssigneeUserId,string Title,string State,string? TaskId);
public sealed record DocumentTaskHistory(Guid DocumentId,IReadOnlyList<DocumentTaskItem> Items,int Total,bool HasPending);
public sealed class DocumentTasks(DocumentDbContext db,IDocumentV2Authority documents,IStaffAuthority staff,ITmsConnector tms,TimeProvider clock)
{
    public async Task<DocumentTaskOptions> OptionsAsync(Guid user,Guid document,CancellationToken ct)
    {
        await VerifyDocument(user,document,ct);var scope=await Staff(user,ct);
        return new(document,user,new[]{new TaskAssignee(user,"Tôi",null,true)}.Concat(scope.ManagedStaff.Where(x=>x.UserId!=user).OrderBy(x=>x.Name,StringComparer.Ordinal).ThenBy(x=>x.UserId).Select(x=>new TaskAssignee(x.UserId,x.Name,x.DepartmentName,false))).ToArray());
    }
    public async Task<DocumentTaskHistory> HistoryAsync(Guid user,Guid document,CancellationToken ct)
    {
        await VerifyDocument(user,document,ct);var query=db.Set<DocumentTaskIntent>().AsNoTracking().Where(x=>x.ActorId==user&&x.DocumentId==document);
        var total=await query.CountAsync(ct);var pending=await query.AnyAsync(x=>x.State=="PendingConfiguration"||x.State=="Preparing"||x.State=="UnknownOutcome",ct);
        var rows=await query.OrderByDescending(x=>x.State=="PendingConfiguration"||x.State=="Preparing"||x.State=="UnknownOutcome").ThenBy(x=>x.Id).Take(50).Select(x=>new DocumentTaskItem(x.Id,x.AssigneeId,x.Title,x.State,x.RemoteTaskId)).ToArrayAsync(ct);
        // Conservative if a concurrent dispatch changed state between the reads.
        return new(document,rows,Math.Max(total,rows.Length),pending||rows.Any(x=>x.State is "PendingConfiguration" or "Preparing" or "UnknownOutcome"));
    }
    public async Task<DocumentTaskReceipt> RetryAsync(Guid user,Guid id,CancellationToken ct)
    {
        var intent=await db.Set<DocumentTaskIntent>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.ActorId==user,ct)??throw Rule(404,"TASK_NOT_FOUND");
        await Verify(user,intent.DocumentId,intent.AssigneeId,ct);
        return await Dispatch(intent,ct);
    }
    public async Task<DocumentTaskReceipt> CreateAsync(Guid user,Guid document,string key,DocumentTaskDraft draft,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(key)||key.Length>128||key.Any(char.IsControl)||draft.AssigneeUserId==Guid.Empty||string.IsNullOrWhiteSpace(draft.Title)||draft.Title.Length>250)throw Rule(400,"INVALID_TASK_REQUEST");
        await Verify(user,document,draft.AssigneeUserId,ct);
        var keyHash=Hash(key);var body=Hash(JsonSerializer.Serialize(new{document,draft}));
        var intent=await db.Set<DocumentTaskIntent>().AsNoTracking().SingleOrDefaultAsync(x=>x.ActorId==user&&x.KeyHash==keyHash,ct);
        if(intent is null)
        {
            intent=new(){DocumentId=document,ActorId=user,AssigneeId=draft.AssigneeUserId,Title=draft.Title,KeyHash=keyHash,BodyHash=body};db.Add(intent);
            try {await db.SaveChangesAsync(ct);db.Entry(intent).State=EntityState.Detached;}
            catch(DbUpdateException){db.Entry(intent).State=EntityState.Detached;intent=await db.Set<DocumentTaskIntent>().AsNoTracking().SingleOrDefaultAsync(x=>x.ActorId==user&&x.KeyHash==keyHash,ct);if(intent is null)throw;}
        }
        if(intent.BodyHash!=body)throw Rule(409,"IDEMPOTENCY_CONFLICT");
        return await Dispatch(intent,ct);
    }
    private async Task<DocumentTaskReceipt> Dispatch(DocumentTaskIntent intent,CancellationToken ct)
    {
        if(intent.State!="PendingConfiguration")return Receipt(intent);
        var token=Guid.NewGuid();var now=clock.GetUtcNow().ToUnixTimeSeconds();
        var claimed=await db.Set<DocumentTaskIntent>().Where(x=>x.Id==intent.Id&&x.State=="PendingConfiguration"&&x.LeaseUntilUnix<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Preparing").SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,now+120).SetProperty(x=>x.Version,x=>x.Version+1),ct);
        if(claimed==1)
        {
            TmsCreateResult response;
            try {using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(60));response=await tms.CreateAsync(intent.Id,intent.DocumentId,intent.ActorId,intent.AssigneeId,intent.Title,deadline.Token);}
            catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException){response=new("UnknownOutcome");}
            await SaveResult(intent.Id,token,response,ct);
        }
        return Receipt(await db.Set<DocumentTaskIntent>().AsNoTracking().SingleAsync(x=>x.Id==intent.Id,ct));
    }
    public async Task<DocumentTaskReceipt> ReconcileAsync(Guid user,Guid id,CancellationToken ct)
    {
        var intent=await db.Set<DocumentTaskIntent>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.ActorId==user,ct)??throw Rule(404,"TASK_NOT_FOUND");
        await Verify(user,intent.DocumentId,intent.AssigneeId,ct);
        var now=clock.GetUtcNow().ToUnixTimeSeconds();var token=Guid.NewGuid();
        var claimed=await db.Set<DocumentTaskIntent>().Where(x=>x.Id==id&&(x.State=="UnknownOutcome"||x.State=="Preparing")&&x.LeaseUntilUnix<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntilUnix,now+120),ct);
        if(claimed==1)
        {
            TmsCreateResult result;
            try {using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(60));result=await tms.ReconcileAsync(id,deadline.Token);}catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException){result=new("UnknownOutcome");}
            // "PendingConfiguration" during reconciliation is not evidence of no remote task.
            if(result.State=="PendingConfiguration")result=new("UnknownOutcome");
            await SaveResult(id,token,result,ct);
        }
        return Receipt(await db.Set<DocumentTaskIntent>().AsNoTracking().SingleAsync(x=>x.Id==id,ct));
    }
    private async Task Verify(Guid user,Guid document,Guid assignee,CancellationToken ct)
    {
        await VerifyDocument(user,document,ct);var scope=await Staff(user,ct);
        if(assignee!=user&&!scope.ManagedStaff.Any(x=>x.UserId==assignee))throw Rule(403,"TASK_ASSIGNEE_FORBIDDEN");
    }
    private async Task VerifyDocument(Guid user,Guid document,CancellationToken ct)
    {
        var docScope=await documents.ReadAsync(user,ct);V2HttpActor.Verify(docScope.Actor,user);
        if(!docScope.ReadableDocumentIds.Contains(document)||!await db.DocumentRegistrations.AnyAsync(x=>x.DocumentId==document&&x.Document!=null,ct))throw Rule(404,"DOCUMENT_NOT_FOUND");
    }
    private async Task<StaffAuthority> Staff(Guid user,CancellationToken ct){var scope=await staff.ResolveAsync(user,ct);StaffAuthorityValidation.Verify(scope,user);return scope;}
    private async Task SaveResult(Guid id,Guid token,TmsCreateResult result,CancellationToken ct)
    {
        var state=result.State;var remote=result.TaskId;
        if(state is not ("Linked" or "PendingConfiguration" or "Rejected")||state=="Linked"&&(string.IsNullOrWhiteSpace(remote)||remote.Length>200)||state!="Linked"&&remote is not null){state="UnknownOutcome";remote=null;}
        await db.Set<DocumentTaskIntent>().Where(x=>x.Id==id&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.RemoteTaskId,remote).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUnix,0L).SetProperty(x=>x.Version,x=>x.Version+1),CancellationToken.None);
    }
    private static DocumentTaskReceipt Receipt(DocumentTaskIntent x)=>new(x.Id,x.State,x.RemoteTaskId);
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static DocumentRegistrationRuleException Rule(int code,string message)=>new(code,message,message);
}
