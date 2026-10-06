using System.Security.Claims;
using Das.PdfProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DocumentService;

[Authorize,ApiController,Route("api/v2/reports/incomplete")]
public sealed class IncompleteReportsController(IReportAuthority authority,IncompleteReports reports):ControllerBase
{
    [HttpGet] public Task<IActionResult> Get([FromQuery]string? kind=null,[FromQuery]Guid? departmentId=null,[FromQuery]bool includeRecent=false,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=20)=>Run(kind,departmentId,includeRecent,pageNumber,pageSize,false);
    [HttpGet("export")] public Task<IActionResult> Export([FromQuery]string? kind=null,[FromQuery]Guid? departmentId=null,[FromQuery]bool includeRecent=false)=>Run(kind,departmentId,includeRecent,1,100,true);
    private async Task<IActionResult> Run(string? kind,Guid? department,bool recent,int page,int size,bool export)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user)||user==Guid.Empty)return Unauthorized();
        try {
            string[] allowed=export?["kind","departmentId","includeRecent"]:["kind","departmentId","includeRecent","pageNumber","pageSize"];
            if(Request.Query.Any(x=>!allowed.Contains(x.Key,StringComparer.Ordinal)||x.Value.Count!=1))throw new DocumentRegistrationRuleException(400,"INVALID_REPORT_QUERY","Invalid report query.");
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var ct=deadline.Token;var scope=await authority.ResolveAsync(user,ct);
            var result=await reports.QueryAsync(user,scope,new(kind is null?null:V2HttpKinds.Parse(kind),department,recent),page,size,export,ct);
            if(!export)return Ok(new{success=true,data=result});
            // Export rechecks authority at the end of potentially slow storage verification.
            var fresh=await authority.ResolveAsync(user,ct);
            if(fresh.UserId!=user||!fresh.IsActive||!fresh.CanReport||!fresh.CanExport||fresh.OrganizationWide!=scope.OrganizationWide||!fresh.DepartmentIds.SetEquals(scope.DepartmentIds)||!fresh.ManagedStaffIds.SetEquals(scope.ManagedStaffIds)||!fresh.ConfidentialDocumentIds.SetEquals(scope.ConfidentialDocumentIds))return StatusCode(409,new{code="REPORT_AUTHORITY_CHANGED"});
            return File(ReportWorkbook.Create(result),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","DAS-incomplete.xlsx");
        }
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
        catch(PdfProtocolException e){return StatusCode(e.Status,new{success=false,code=e.Code});}
        catch(OperationCanceledException) when(!HttpContext.RequestAborted.IsCancellationRequested){return StatusCode(503,new{code="REPORT_TIMEOUT"});}
        catch(Exception e) when(e is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException or IOException){return StatusCode(503,new{code="REPORT_DEPENDENCY_UNAVAILABLE"});}
    }
}
