using System.Security.Claims;
using Das.PdfProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

[Authorize,ApiController,Route("api/v2/documents/{documentId:guid}/pdf")]
public sealed class CurrentPdfController(CurrentPdfService pdf,DocumentDbContext db,IPdfAuthority authority,IPdfFilesClient files):ControllerBase
{
    [HttpPut]
    public Task<IActionResult> Replace(Guid documentId,PdfReplaceDraft draft)=>Run(async user=> {
        var actor=await authority.EditorAsync(documentId,user,HttpContext.RequestAborted);
        if(actor.UserId!=user)throw new DocumentRegistrationRuleException(503,"AUTHORITY_MISMATCH","The authority response is invalid.");
        var result=await pdf.ReplaceAsync(documentId,draft,actor,HttpContext.RequestAborted);
        var op=await pdf.OperationAsync(result.OperationId,HttpContext.RequestAborted);
        return StatusCode(op.State=="Desired"?202:200,new{success=true,data=result,state=op.State});
    });
    [HttpGet]
    public Task<IActionResult> Info(Guid documentId)=>Run(async user=> {
        if(!await authority.CanReadAsync(documentId,user,HttpContext.RequestAborted))return NotFound();
        if(!await db.Documents.AnyAsync(x=>x.Id==documentId && !x.IsDeleted,HttpContext.RequestAborted))return NotFound();
        var link=await db.Set<DocumentCurrentPdf>().AsNoTracking().SingleOrDefaultAsync(x=>x.DocumentId==documentId,HttpContext.RequestAborted);
        if(link is null)return NotFound();
        if(link.State!="Ready")return StatusCode(423,new{code="PDF_UNAVAILABLE"});
        var receipt=await files.InspectAsync(link.OperationId,HttpContext.RequestAborted);
        if(receipt.OperationId!=link.OperationId || receipt.DocumentId!=documentId || receipt.FileId!=link.FileId || receipt.Sha256!=link.Sha256 || receipt.SizeBytes!=link.SizeBytes)
            throw new PdfProtocolException(503,"PDF_RECEIPT_CONFLICT");
        if(receipt.State!="Active")return StatusCode(423,new{code="PDF_UNAVAILABLE"});
        var document=await db.Documents.AsNoTracking().Include(x=>x.Registration).SingleAsync(x=>x.Id==documentId,HttpContext.RequestAborted);
        var completion=DocumentCompletionEvaluator.Evaluate(document.Status,true,document.Registration!.IssuedDate);
        return Ok(new{success=true,data=new{link.FileId,link.OriginalName,link.SizeBytes,link.Sha256,contentType="application/pdf",canDownload=true,completion}});
    });
    private async Task<IActionResult> Run(Func<Guid,Task<IActionResult>> body)
    {
        Response.Headers.CacheControl="no-store";
        var sub=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");
        if(!Guid.TryParse(sub,out var user) || user==Guid.Empty)return Unauthorized();
        try{return await body(user);}
        catch(DocumentRegistrationRuleException e){return StatusCode(e.Status,new{code=e.Code});}
        catch(PdfProtocolException e){return StatusCode(e.Status,new{code=e.Code});}
        catch(Exception e) when(e is IOException or InvalidOperationException or DbUpdateException){return StatusCode(503,new{code="PDF_SERVICE_UNAVAILABLE"});}
    }
}
