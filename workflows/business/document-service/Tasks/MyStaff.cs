namespace DocumentService;

public sealed record StaffMember(Guid UserId,string Name,Guid DepartmentId,string DepartmentName);
public sealed record StaffAuthority(Guid UserId,bool IsActive,IReadOnlyList<StaffMember> ManagedStaff);
public static class StaffAuthorityValidation
{
    public static void Verify(StaffAuthority scope,Guid user)
    {
        if(scope.UserId!=user||scope.ManagedStaff.Count>2000||scope.ManagedStaff.Any(x=>x.UserId==Guid.Empty||x.DepartmentId==Guid.Empty||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>200||string.IsNullOrWhiteSpace(x.DepartmentName)||x.DepartmentName.Length>200)||scope.ManagedStaff.GroupBy(x=>x.UserId).Any(x=>x.Count()!=1))throw new DocumentRegistrationRuleException(503,"AUTHORITY_MISMATCH","Staff authority is inconsistent.");
        if(!scope.IsActive)throw new DocumentRegistrationRuleException(403,"ACTOR_INACTIVE","Inactive actor.");
    }
}
public interface IStaffAuthority {Task<StaffAuthority> ResolveAsync(Guid user,CancellationToken ct);}
public sealed class UnavailableStaffAuthority:IStaffAuthority
{public Task<StaffAuthority> ResolveAsync(Guid user,CancellationToken ct)=>throw new DocumentRegistrationRuleException(503,"STAFF_AUTHORITY_UNAVAILABLE","Staff authority is not connected.");}
public sealed record StaffTask(string TaskId,Guid AssigneeUserId,string Title,string Status,DateTimeOffset? DueAt);
public sealed record StaffTaskPage(IReadOnlyList<StaffTask> Items,int Total,int PageNumber,int PageSize);
// This is a DAS contract, not a claim about the unprovided TMS API.
// The future adapter resolves remote IDs and enforces the exact assignee scope.
public interface ITmsConnector
{
    Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users,int page,int size,CancellationToken ct);
    Task<TmsCreateResult> CreateAsync(Guid correlation,Guid document,Guid actor,Guid assignee,string title,CancellationToken ct);
    Task<TmsCreateResult> ReconcileAsync(Guid correlation,CancellationToken ct);
}
public sealed record TmsCreateResult(string State,string? TaskId=null);
public sealed class UnavailableTmsConnector:ITmsConnector
{
    public Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users,int page,int size,CancellationToken ct)=>throw Missing();
    public Task<TmsCreateResult> CreateAsync(Guid correlation,Guid document,Guid actor,Guid assignee,string title,CancellationToken ct)=>Task.FromResult(new TmsCreateResult("PendingConfiguration"));
    public Task<TmsCreateResult> ReconcileAsync(Guid correlation,CancellationToken ct)=>Task.FromResult(new TmsCreateResult("PendingConfiguration"));
    private static DocumentRegistrationRuleException Missing()=>new(503,"TMS_UNAVAILABLE","TMS is not connected.");
}
public sealed record MyStaffPage(IReadOnlyList<StaffMember> Staff,int Total,int PageNumber,int PageSize,StaffTaskPage? Tasks,string TaskState);
public sealed class MyStaffService(IStaffAuthority authority,ITmsConnector tms)
{
    public async Task<MyStaffPage> QueryAsync(Guid user,int page,int size,bool tasks,CancellationToken ct)
    {
        if(page<1||page>1000000||size<1||size>100)throw new DocumentRegistrationRuleException(400,"INVALID_STAFF_QUERY","Invalid staff query.");
        var scope=await authority.ResolveAsync(user,ct);
        StaffAuthorityValidation.Verify(scope,user);
        var staff=scope.ManagedStaff.OrderBy(x=>x.Name,StringComparer.Ordinal).ThenBy(x=>x.UserId).ToArray();
        StaffTaskPage? result=null;var state=tasks?"Unavailable":"NotRequested";
        if(tasks&&staff.Length>0)
        {
            try {
                var ids=staff.Select(x=>x.UserId).ToHashSet();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(15));result=await tms.ListAsync(ids,page,size,deadline.Token);
                if(result.PageNumber!=page||result.PageSize!=size||result.Total<result.Items.Count||result.Items.Count>size||result.Items.Any(x=>!ids.Contains(x.AssigneeUserId)||string.IsNullOrWhiteSpace(x.TaskId)||string.IsNullOrWhiteSpace(x.Title)||string.IsNullOrWhiteSpace(x.Status)))throw new DocumentRegistrationRuleException(503,"TMS_RESPONSE_INVALID","Invalid TMS response.");
                state="Connected";
            }
            catch(DocumentRegistrationRuleException e) when(e.Status==503){result=null;state=e.Code;}
            catch(HttpRequestException){result=null;state="TMS_UNAVAILABLE";}
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){result=null;state="TMS_TIMEOUT";}
        }
        return new(staff.Skip(checked((page-1)*size)).Take(size).ToArray(),staff.Length,page,size,result,state);
    }
}
