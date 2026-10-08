using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DocumentService;
[Authorize,ApiController,Route("api/v2/my-staff")]
public sealed class MyStaffController(MyStaffService staff):ControllerBase
{
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        var query=Request.Query;
        if(query.Any(x=>x.Key is not ("pageNumber" or "pageSize" or "assigneeUserId")||x.Value.Count!=1||string.IsNullOrWhiteSpace(x.Value[0])))return InvalidTaskQuery();
        var page=1;var size=20;Guid? assignee=null;
        if(query.TryGetValue("pageNumber",out var pageText)&&!int.TryParse(pageText[0],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out page))return InvalidTaskQuery();
        if(query.TryGetValue("pageSize",out var sizeText)&&!int.TryParse(sizeText[0],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out size))return InvalidTaskQuery();
        if(query.TryGetValue("assigneeUserId",out var assigneeText))
        {
            if(assigneeText[0]!.Length!=36||!Guid.TryParseExact(assigneeText[0],"D",out var id)||id==Guid.Empty)return InvalidTaskQuery();
            assignee=id;
        }
        try{return Ok(new{success=true,data=await staff.QueryTasksAsync(user,page,size,assignee,HttpContext.RequestAborted)});}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
    }
    private IActionResult InvalidTaskQuery()=>BadRequest(new{success=false,code="INVALID_STAFF_QUERY"});

    [HttpGet] public async Task<IActionResult> Get([FromQuery]int pageNumber=1,[FromQuery]int pageSize=20,[FromQuery]bool includeTasks=true)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        if(Request.Query.Any(x=>x.Key is not ("pageNumber" or "pageSize" or "includeTasks")||x.Value.Count!=1))return BadRequest(new{code="INVALID_STAFF_QUERY"});
        try {return Ok(new{success=true,data=await staff.QueryAsync(user,pageNumber,pageSize,includeTasks,HttpContext.RequestAborted)});}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
    }
}
