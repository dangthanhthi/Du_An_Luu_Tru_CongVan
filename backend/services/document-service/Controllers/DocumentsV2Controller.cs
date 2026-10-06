using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

[Authorize,ApiController,Route("api/v2/documents")]
[RequestSizeLimit(131072)]
public sealed class DocumentsV2Controller(DocumentDbContext db,IDocumentV2Authority authority,V2RegistrationService registration,V2DocumentEditor editor,V2DocumentLifecycle lifecycle,V2DocumentQueries queries):ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Register(RegisterDocumentRequest request)=>Run(async user=>{
        var keys=Request.Headers["Idempotency-Key"];if(keys.Count!=1||string.IsNullOrEmpty(keys[0]))throw Rule(400,"INVALID_IDEMPOTENCY_KEY");
        var draft=request.Draft();if(draft.Kind=="INCOMING"&&draft.Details is null)throw Rule(400,"INVALID_KIND_DETAILS");
        var context=await authority.RegisterAsync(user,draft,HttpContext.RequestAborted);V2HttpActor.Verify(context.Actor,user);
        if(context.Identity.InputterUserId!=user||context.Identity.OriginatorUserId!=draft.OriginatorUserId||context.Identity.OwnerDepartmentId!=draft.OwnerDepartmentId)throw Rule(503,"AUTHORITY_MISMATCH");
        if(!context.Actor.DepartmentIds.Contains(draft.OwnerDepartmentId))throw Rule(403,"DESTINATION_FORBIDDEN");
        var doc=await registration.RegisterAsync(draft,context.Identity,keys[0]!,HttpContext.RequestAborted,context.References,context.Relations);
        return Ok(new{success=true,data=Result(doc)});
    });
    [HttpPut("{documentId:guid}")]
    public Task<IActionResult> Edit(Guid documentId,EditDocumentRequest request)=>Run(async user=>{
        var draft=request.Draft();var context=await authority.MutateAsync(user,documentId,draft,HttpContext.RequestAborted);V2HttpActor.Verify(context.Actor,user);
        var doc=await editor.UpdateAsync(documentId,draft,context.Actor,context.Target,HttpContext.RequestAborted,context.References,context.Relations);
        return Ok(new{success=true,data=Result(doc)});
    });
    [HttpPost("{documentId:guid}/status")]
    public Task<IActionResult> Status(Guid documentId,ChangeDocumentStatusRequest request)=>Run(async user=>{
        var draft=request.Draft();var context=await authority.MutateAsync(user,documentId,null,HttpContext.RequestAborted);V2HttpActor.Verify(context.Actor,user);
        var doc=await lifecycle.ChangeAsync(documentId,draft,context.Actor,HttpContext.RequestAborted);return Ok(new{success=true,data=Result(doc)});
    });
    [HttpGet]
    public Task<IActionResult> List([FromQuery]string kind,[FromQuery]string view="all",[FromQuery]string? status=null,[FromQuery]string? searchTerm=null,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=20)=>Run(async user=>{
        ValidateQuery(["kind","view","status","searchTerm","pageNumber","pageSize"]);var k=V2HttpKinds.Parse(kind);
        if(view is not ("all" or "mine" or "department" or "cancelled")||pageNumber<1||pageNumber>1000000||pageSize<1||pageSize>100||searchTerm?.Length>200||(!string.IsNullOrEmpty(status)&&status is not ("InProgress" or "Distributed" or "Cancelled"))||view=="cancelled"&&!string.IsNullOrEmpty(status)&&status!="Cancelled")throw Rule(400,"INVALID_DOCUMENT_QUERY");
        var scope=await authority.ReadAsync(user,HttpContext.RequestAborted);V2HttpActor.Verify(scope.Actor,user);
        return Ok(new{success=true,data=await queries.ListAsync(k,view,status,searchTerm,pageNumber,pageSize,scope,HttpContext.RequestAborted)});
    });
    [HttpGet("{documentId:guid}")]
    public Task<IActionResult> Detail(Guid documentId)=>Run(async user=>{
        ValidateQuery([]);var scope=await authority.ReadAsync(user,HttpContext.RequestAborted);V2HttpActor.Verify(scope.Actor,user);
        return Ok(new{success=true,data=await queries.DetailAsync(documentId,scope,HttpContext.RequestAborted)});
    });
    [HttpGet("options")]
    public Task<IActionResult> Options([FromQuery]string kind)=>Run(async user=>{
        ValidateQuery(["kind"]);var k=V2HttpKinds.Parse(kind);var form=await authority.FormAsync(user,k,HttpContext.RequestAborted);
        if(form.UserId!=user)throw Rule(503,"AUTHORITY_MISMATCH");if(!form.IsActive)throw Rule(403,"ACTOR_INACTIVE");
        if(form.Targets.Count>2000||form.Targets.Any(x=>x.DepartmentId==Guid.Empty||x.OriginatorUserId==Guid.Empty||string.IsNullOrWhiteSpace(x.DepartmentCode)||string.IsNullOrWhiteSpace(x.DepartmentName)||string.IsNullOrWhiteSpace(x.OriginatorName)))throw Rule(503,"AUTHORITY_MISMATCH");
        var catalogs=await db.BusinessCatalogEntries.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Group).ThenBy(x=>x.Code).Select(x=>new{x.Group,x.Code,x.Name}).ToListAsync(HttpContext.RequestAborted);
        var targets=await db.DistributionTargets.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name}).ToListAsync(HttpContext.RequestAborted);
        return Ok(new{success=true,data=new{kind=V2HttpKinds.Display(k),form.UserId,form.CanRegister,targets=form.Targets,catalogs,distributionTargets=targets}});
    });
    private void ValidateQuery(string[] allowed){if(Request.Query.Any(x=>!allowed.Contains(x.Key,StringComparer.Ordinal)||x.Value.Count!=1))throw Rule(400,"INVALID_DOCUMENT_QUERY");}
    private async Task<IActionResult> Run(Func<Guid,Task<IActionResult>> action)
    {
        Response.Headers.CacheControl="no-store";var sub=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!Guid.TryParse(sub,out var user)||user==Guid.Empty)return Unauthorized();
        try{return await action(user);}catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
        catch(ArgumentException){return BadRequest(new{success=false,code="INVALID_DOCUMENT_REQUEST"});}
        catch(Exception e)when(e is IOException or InvalidOperationException or DbUpdateException or System.Data.Common.DbException or HttpRequestException){return StatusCode(503,new{success=false,code="DOCUMENT_SERVICE_UNAVAILABLE"});}
    }
    private static V2WriteResult Result(Document d)=>new(d.Id,d.DocumentNumber,d.Registration!.Version,d.Status);
    private static DocumentRegistrationRuleException Rule(int status,string code)=>new(status,code,code);
}
