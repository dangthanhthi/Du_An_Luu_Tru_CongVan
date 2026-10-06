using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;
public sealed record ReminderOperator(Guid UserId,bool IsActive,IReadOnlySet<Guid> DepartmentIds);
public interface IReminderOperatorAuthority{Task<ReminderOperator> ResolveAsync(Guid user,CancellationToken ct);}
public sealed class UnavailableReminderOperatorAuthority:IReminderOperatorAuthority
{public Task<ReminderOperator> ResolveAsync(Guid user,CancellationToken ct)=>throw new DocumentRegistrationRuleException(503,"REMINDER_OPERATOR_UNAVAILABLE","Reminder operations authority is not connected.");}
[Authorize(Policy="ReminderOperate"),ApiController,Route("api/v2/reminders")]
public sealed class ReminderOperationsController(DocumentDbContext db,WeeklyReminders reminders,IReminderOperatorAuthority operators):ControllerBase
{
    [HttpPost("departments/{departmentId:guid}/run")]
    public Task<IActionResult> Run(Guid departmentId)=>Action(async scope=>{
        if(!scope.DepartmentIds.Contains(departmentId))throw new DocumentRegistrationRuleException(403,"REMINDER_SCOPE_FORBIDDEN","Department unavailable.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var batch=await reminders.PlanAsync(departmentId,deadline.Token);var state=await reminders.DispatchAsync(batch.Id,deadline.Token);
        return Accepted(new{success=true,data=new{batch.Id,batch.DepartmentId,batch.Period,state}});
    });
    [HttpGet("runs")]
    public Task<IActionResult> List([FromQuery]int pageNumber=1,[FromQuery]int pageSize=20)=>Action(async scope=>{
        if(pageNumber<1||pageNumber>1000000||pageSize<1||pageSize>100||Request.Query.Any(x=>x.Key is not ("pageNumber" or "pageSize")||x.Value.Count!=1))return BadRequest(new{code="INVALID_REMINDER_QUERY"});
        var ids=scope.DepartmentIds.ToArray();var query=db.Set<ReminderBatch>().AsNoTracking().Where(x=>ids.Contains(x.DepartmentId));
        var total=await query.CountAsync(HttpContext.RequestAborted);var items=await query.OrderByDescending(x=>x.Period).ThenBy(x=>x.DepartmentId).Skip(checked((pageNumber-1)*pageSize)).Take(pageSize).Select(x=>new{x.Id,x.DepartmentId,x.Period,x.State,x.Attempts,x.Version}).ToArrayAsync(HttpContext.RequestAborted);
        return Ok(new{success=true,data=new{items,total,pageNumber,pageSize}});
    });
    [HttpGet("runs/{id:guid}/deliveries")]
    public Task<IActionResult> Deliveries(Guid id,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=20)=>Action(async scope=>{
        if(pageNumber<1||pageNumber>1000000||pageSize<1||pageSize>100||Request.Query.Any(x=>x.Key is not ("pageNumber" or "pageSize")||x.Value.Count!=1))return BadRequest(new{code="INVALID_REMINDER_QUERY"});
        var batch=await db.Set<ReminderBatch>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,HttpContext.RequestAborted);
        if(batch is null||!scope.DepartmentIds.Contains(batch.DepartmentId))return NotFound(new{code="REMINDER_NOT_FOUND"});
        var query=db.Set<ReminderDelivery>().AsNoTracking().Where(x=>x.BatchId==id);var total=await query.CountAsync(HttpContext.RequestAborted);
        var items=await query.OrderBy(x=>x.Id).Skip(checked((pageNumber-1)*pageSize)).Take(pageSize).Select(x=>new{x.Id,x.State,x.Attempts,x.Failures,x.NotificationId,x.NotificationState,x.Version}).ToArrayAsync(HttpContext.RequestAborted);
        return Ok(new{success=true,data=new{items,total,pageNumber,pageSize}});
    });
    private async Task<IActionResult> Action(Func<ReminderOperator,Task<IActionResult>> action)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        try {var scope=await operators.ResolveAsync(user,HttpContext.RequestAborted);if(scope.UserId!=user)throw new DocumentRegistrationRuleException(503,"AUTHORITY_MISMATCH","Invalid operations authority.");if(!scope.IsActive)return Forbid();return await action(scope);}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
        catch(OperationCanceledException) when(!HttpContext.RequestAborted.IsCancellationRequested){return StatusCode(503,new{code="REMINDER_TIMEOUT"});}
    }
}
