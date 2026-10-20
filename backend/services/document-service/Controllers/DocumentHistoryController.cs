using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

[Authorize,ApiController,Route("api/v2/documents")]
public sealed class DocumentHistoryController(IDocumentV2Authority authority,V2LifecycleHistory history):ControllerBase
{
    [HttpGet("{documentId:guid}/history")]
    public async Task<IActionResult> Read(Guid documentId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var user) || user==Guid.Empty) return Unauthorized();
        try {
            var allowed=new[]{"pageNumber","pageSize","throughVersion"};
            if(Request.Query.Any(x=>!allowed.Contains(x.Key,StringComparer.Ordinal) || x.Value.Count!=1)) throw V2LifecycleHistory.InvalidQuery();
            var page=ParseInt("pageNumber",1,1_000_000); var size=ParseInt("pageSize",20,100); long? watermark=null;
            if(Request.Query.TryGetValue("throughVersion",out var raw)) {
                var value=raw.ToString();
                if(value.Length is <1 or >19 || value[0]=='0' || value.Any(c=>c<'0'||c>'9') || !long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed) || parsed<1) throw V2LifecycleHistory.InvalidQuery();
                watermark=parsed;
            }
            var scope=await authority.ReadAsync(user,ct); V2HttpActor.Verify(scope.Actor,user);
            var result=await history.ReadAsync(documentId,page,size,watermark,scope,ct);
            // No database transaction is held while revalidating authority across a network boundary.
            var fresh=await authority.ReadAsync(user,ct); V2HttpActor.Verify(fresh.Actor,user);
            if(!fresh.ReadableDocumentIds.Contains(documentId)) throw V2LifecycleHistory.Missing();
            return Ok(new {success=true,data=result});
        }
        catch(DocumentRegistrationRuleException e) {return StatusCode(e.Status,new {success=false,code=e.Code});}
        catch(Exception e) when(e is IOException or InvalidOperationException or DbUpdateException or System.Data.Common.DbException or HttpRequestException or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException) {
            return StatusCode(503,new {success=false,code="DOCUMENT_SERVICE_UNAVAILABLE"});
        }
    }
    private int ParseInt(string key,int fallback,int max)
    {
        if(!Request.Query.TryGetValue(key,out var raw)) return fallback;
        var value=raw.ToString();
        if(value.Length is <1 or >7 || value.Any(c=>c<'0'||c>'9') || !int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed) || parsed<1 || parsed>max) throw V2LifecycleHistory.InvalidQuery();
        return parsed;
    }
}
