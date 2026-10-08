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
// ListAsync filters the allowed user set BEFORE counting and paging. Items and total
// must come from the same query snapshot in a stable adapter-defined order across
// pages. DAS cannot infer ordering from opaque task IDs; real TMS must certify it.
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
    public async Task<MyStaffTasksResult> QueryTasksAsync(Guid user,int page,int size,Guid? assignee,CancellationToken ct)
    {
        if(page<1||page>1000000||size<1||size>100||assignee==Guid.Empty)throw new DocumentRegistrationRuleException(400,"INVALID_STAFF_QUERY","Invalid task query.");
        ct.ThrowIfCancellationRequested();
        var initial=await ResolveTaskScope(user,ct);
        var managedIds=initial.ManagedStaff.Select(x=>x.UserId).ToHashSet();
        if(assignee.HasValue&&!managedIds.Contains(assignee.Value))throw new DocumentRegistrationRuleException(403,"ASSIGNEE_FORBIDDEN","Assignee is outside managed scope.");
        var allowed=assignee.HasValue?new HashSet<Guid>{assignee.Value}:managedIds;
        StaffTaskPage? remote=null;var state="Connected";
        if(allowed.Count>0)
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                remote=await tms.ListAsync(allowed,page,size,deadline.Token).WaitAsync(deadline.Token);
                ct.ThrowIfCancellationRequested();
                if(!ValidTaskPage(remote,allowed,page,size)){remote=null;state="TMS_CONTRACT_INVALID";}
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(OperationCanceledException){state="TMS_TIMEOUT";remote=null;}
            catch(TimeoutException){state="TMS_TIMEOUT";remote=null;}
            catch(System.Text.Json.JsonException){state="TMS_CONTRACT_INVALID";remote=null;}
            catch(DocumentRegistrationRuleException e) when(e.Status==503)
            {state=e.Code is "TMS_CONTRACT_INVALID" or "TMS_RESPONSE_INVALID"?"TMS_CONTRACT_INVALID":e.Code=="TMS_TIMEOUT"?"TMS_TIMEOUT":"TMS_UNAVAILABLE";remote=null;}
            catch(Exception){ct.ThrowIfCancellationRequested();state="TMS_UNAVAILABLE";remote=null;}
        }
        // Revalidate even degraded responses and verified empty scopes before
        // disclosing a selected staff member or any task/name from the old scope.
        ct.ThrowIfCancellationRequested();
        var fresh=await ResolveTaskScope(user,ct);
        ct.ThrowIfCancellationRequested();
        var members=fresh.ManagedStaff.ToDictionary(x=>x.UserId);
        if(!managedIds.SetEquals(members.Keys))throw new DocumentRegistrationRuleException(409,"STAFF_SCOPE_CHANGED","Managed staff changed during the request.");
        MyStaffTasksPage? tasks=state!="Connected"?null:remote is null?new([],0,page,size):new(
            remote.Items.Select(x=>new MyStaffTaskItem(x.TaskId,members[x.AssigneeUserId],x.Title,x.Status,x.DueAt?.UtcDateTime)).ToArray(),remote.Total,page,size);
        return new(state,assignee.HasValue?members[assignee.Value]:null,tasks);
    }

    private async Task<StaffAuthority> ResolveTaskScope(Guid user,CancellationToken ct)
    {
        StaffAuthority scope;
        try{scope=await authority.ResolveAsync(user,ct);}
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(DocumentRegistrationRuleException){throw;}
        catch(Exception){ct.ThrowIfCancellationRequested();throw new DocumentRegistrationRuleException(503,"STAFF_AUTHORITY_UNAVAILABLE","Staff authority is unavailable.");}
        if(scope is null||scope.ManagedStaff is null||scope.ManagedStaff.Any(x=>x is null))throw new DocumentRegistrationRuleException(503,"AUTHORITY_MISMATCH","Staff authority is inconsistent.");
        StaffAuthorityValidation.Verify(scope,user);
        return scope;
    }

    private static bool ValidTaskPage(StaffTaskPage? result,IReadOnlySet<Guid> allowed,int page,int size)
    {
        if(result is null||result.Items is null||result.PageNumber!=page||result.PageSize!=size||result.Total<0||result.Items.Count>size||result.Total<result.Items.Count)return false;
        var offset=(long)(page-1)*size;
        if(offset>=result.Total&&result.Items.Count!=0||offset<result.Total&&offset+result.Items.Count>result.Total)return false;
        var taskIds=new HashSet<string>(StringComparer.Ordinal);
        return result.Items.All(x=>x is not null&&allowed.Contains(x.AssigneeUserId)&&
            !string.IsNullOrWhiteSpace(x.TaskId)&&x.TaskId.Length<=200&&taskIds.Add(x.TaskId)&&
            !string.IsNullOrWhiteSpace(x.Title)&&x.Title.Length<=2000&&
            !string.IsNullOrWhiteSpace(x.Status)&&x.Status.Length<=100&&
            (!x.DueAt.HasValue||x.DueAt.Value.Offset==TimeSpan.Zero));
    }

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
