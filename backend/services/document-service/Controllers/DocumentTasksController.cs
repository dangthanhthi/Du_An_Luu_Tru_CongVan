using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DocumentService;
[Authorize,ApiController,Route("api/v2")]
[RequestSizeLimit(4096)]
public sealed class DocumentTasksController(DocumentTasks tasks):ControllerBase
{
    [HttpGet("documents/{documentId:guid}/tasks/options")] public Task<IActionResult> Options(Guid documentId)=>Run(user=>tasks.OptionsAsync(user,documentId,HttpContext.RequestAborted));
    [HttpGet("documents/{documentId:guid}/tasks")] public Task<IActionResult> History(Guid documentId)=>Run(user=>tasks.HistoryAsync(user,documentId,HttpContext.RequestAborted));
    [HttpPost("documents/{documentId:guid}/tasks")] public Task<IActionResult> Create(Guid documentId,DocumentTaskDraft draft)=>Run(user=>{
        var keys=Request.Headers["Idempotency-Key"];if(keys.Count!=1)throw new DocumentRegistrationRuleException(400,"INVALID_IDEMPOTENCY_KEY","An idempotency key is required.");
        return tasks.CreateAsync(user,documentId,keys[0]!,draft,HttpContext.RequestAborted);
    });
    [HttpPost("task-intents/{id:guid}/reconcile")] public Task<IActionResult> Reconcile(Guid id)=>Run(user=>tasks.ReconcileAsync(user,id,HttpContext.RequestAborted));
    [HttpPost("task-intents/{id:guid}/retry")] public Task<IActionResult> Retry(Guid id)=>Run(user=>tasks.RetryAsync(user,id,HttpContext.RequestAborted));
    private async Task<IActionResult> Run<T>(Func<Guid,Task<T>> action)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        try {
            if(Request.Query.Count!=0)throw new DocumentRegistrationRuleException(400,"INVALID_TASK_QUERY","Unexpected task query.");
            var data=await action(user);return StatusCode(data is DocumentTaskReceipt receipt&&receipt.State!="Linked"?202:200,new{success=true,data});}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
    }
}
