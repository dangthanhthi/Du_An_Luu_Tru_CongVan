using System.Security.Claims;
using Das.PdfProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

[ApiController,Route("internal/pdf"),InternalPdfKey]
public sealed class PdfProtocolController(CurrentPdfService pdf,DocumentDbContext db,IPdfAuthority authority):ControllerBase
{
    [HttpGet("{id:guid}/operation")]
    public async Task<IActionResult> Operation(Guid id)
    {
        Response.Headers.CacheControl="no-store";
        try{return Ok(await pdf.OperationAsync(id,HttpContext.RequestAborted));}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{code=e.Code});}
    }
    [Authorize,HttpGet("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id,Guid documentId,Guid fileId)
    {
        Response.Headers.CacheControl="no-store";
        var sub=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");
        if(!Guid.TryParse(sub,out var user) || user==Guid.Empty)return Unauthorized();
        try {
            // Both link and resource authority are fresh on every metadata/content request.
            var current=await db.Set<DocumentCurrentPdf>().AsNoTracking().SingleOrDefaultAsync(x=>x.DocumentId==documentId && x.FileId==fileId && x.OperationId==id && x.State=="Ready",HttpContext.RequestAborted);
            if(current is null || !await db.Documents.AnyAsync(x=>x.Id==documentId && !x.IsDeleted,HttpContext.RequestAborted))return NotFound();
            if(!await authority.CanReadAsync(documentId,user,HttpContext.RequestAborted))return NotFound();
            return Ok(new{allowed=true});
        } catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{code=e.Code});}
    }
}
