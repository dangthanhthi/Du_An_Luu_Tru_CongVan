using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PartnerService;

[ApiController, Route("api/partners"), Authorize(Policy="CatalogManage")]
public sealed class PartnerHistoryController(PartnerAuditQuery query) : ControllerBase
{
    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        // Check the subject before looking up an ID, including IDs that do not exist.
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),out var actor) || actor == Guid.Empty)
            return Failure(403,"PARTNER_CATALOG_FORBIDDEN","Không có quyền xem lịch sử đơn vị.");
        try {
            var allowed = new[] {"pageNumber","pageSize","throughVersion"};
            if (Request.Query.Any(x => !allowed.Contains(x.Key) || x.Value.Count != 1)) throw InvalidQuery();
            var page = ParseInt("pageNumber",1,1_000_000); var size = ParseInt("pageSize",20,100);
            long? watermark = null;
            if (Request.Query.TryGetValue("throughVersion",out var raw)) {
                var value = raw.ToString();
                if (value.Length is < 1 or > 19 || value[0] == '0' || value.Any(c => c < '0' || c > '9') ||
                    !long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed) || parsed < 1) throw InvalidQuery();
                watermark = parsed;
            }
            return Ok(new {success=true,data=await query.ReadAsync(id,page,size,watermark,ct)});
        }
        catch(PartnerRuleException e) {return Failure(e.Status,e.Code,e.Message);}
        catch(Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException) {return Failure(503,"PARTNER_DEPENDENCY_UNAVAILABLE","Lịch sử đơn vị đang không khả dụng.");}
        catch(System.Data.Common.DbException) {return Failure(503,"PARTNER_DEPENDENCY_UNAVAILABLE","Lịch sử đơn vị đang không khả dụng.");}
    }
    private int ParseInt(string key,int fallback,int max)
    {
        if (!Request.Query.TryGetValue(key,out var raw)) return fallback;
        var value=raw.ToString();
        if(value.Length is < 1 or > 7 || value.Any(c => c < '0' || c > '9') || !int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed) || parsed < 1 || parsed > max) throw InvalidQuery();
        return parsed;
    }
    private static PartnerRuleException InvalidQuery()=>new(400,"INVALID_HISTORY_QUERY","Tham số lịch sử không hợp lệ.");
    private IActionResult Failure(int status,string code,string message)=>StatusCode(status,new {success=false,data=(object?)null,message,errors=new[]{new {field="",code,message}},traceId=HttpContext.TraceIdentifier});
}
