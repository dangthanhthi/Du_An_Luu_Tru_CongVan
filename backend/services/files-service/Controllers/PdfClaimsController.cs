using Das.PdfProtocol;
using FilesService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FilesService.Controllers;

[ApiController,Route("internal/pdf"),InternalPdfKey]
public sealed class PdfClaimsController(PdfClaims claims):ControllerBase
{
    [HttpPost("prepare")] public Task<IActionResult> Prepare(PdfPrepare request)=>Run(async()=>Ok(await claims.PrepareAsync(request,HttpContext.RequestAborted)));
    [HttpPost("{id:guid}/activate")] public Task<IActionResult> Activate(Guid id)=>Run(async()=>Ok(await claims.ActivateAsync(id,HttpContext.RequestAborted)));
    [HttpPost("{id:guid}/retire")] public Task<IActionResult> Retire(Guid id)=>Run(async()=>{await claims.RetireAsync(id,HttpContext.RequestAborted);return Ok(new{retired=true});});
    [HttpGet("{id:guid}/info")] public Task<IActionResult> Info(Guid id)=>Run(async()=>Ok(await claims.InspectAsync(id,HttpContext.RequestAborted)));
    private async Task<IActionResult> Run(Func<Task<IActionResult>> body)
    {
        Response.Headers.CacheControl="no-store";
        try{return await body();}
        catch(FileRuleException e){return StatusCode(e.Status,new{code=e.Code});}
        catch(PdfProtocolException e){return StatusCode(e.Status,new{code=e.Code});}
        catch(Exception e) when(e is IOException or InvalidOperationException or DbUpdateException){return StatusCode(503,new{code="PDF_SERVICE_UNAVAILABLE"});}
    }
}
