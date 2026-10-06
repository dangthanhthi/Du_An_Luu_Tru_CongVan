using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DocumentService;
[Authorize,ApiController,Route("api/v2/my-staff")]
public sealed class MyStaffController(MyStaffService staff):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery]int pageNumber=1,[FromQuery]int pageSize=20,[FromQuery]bool includeTasks=true)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        if(Request.Query.Any(x=>x.Key is not ("pageNumber" or "pageSize" or "includeTasks")||x.Value.Count!=1))return BadRequest(new{code="INVALID_STAFF_QUERY"});
        try {return Ok(new{success=true,data=await staff.QueryAsync(user,pageNumber,pageSize,includeTasks,HttpContext.RequestAborted)});}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
    }
}
