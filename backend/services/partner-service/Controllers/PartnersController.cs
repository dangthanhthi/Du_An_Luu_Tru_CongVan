using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PartnerService;

[ApiController, Route("api/partners"), Authorize, RequestSizeLimit(32768)]
public sealed class PartnersController(IPartnerBusinessService service) : ControllerBase
{
    private bool CanManage=>User.HasClaim("das_capability","CatalogManage")&&Actor()!=Guid.Empty;
    private Guid Actor()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:Guid.Empty;
    [HttpGet("options")]
    public IActionResult Options(){Response.Headers.CacheControl="no-store";return Ok(new{success=true,data=new{canManage=CanManage}});}
    [HttpGet]
    public async Task<IActionResult> List(string? searchTerm=null,string? entityType=null,bool? isActive=null,int pageNumber=1,int pageSize=10,bool includeDeleted=false,CancellationToken ct=default)
    {
        if(includeDeleted&&!CanManage)return Failure(403,"PARTNER_CATALOG_FORBIDDEN","Không có quyền xem danh sách đã xóa.");
        return await Run(async()=>{
            var allowed=new[]{"searchTerm","entityType","isActive","pageNumber","pageSize","includeDeleted"};
            if(Request.Query.Any(x=>!allowed.Contains(x.Key)||x.Value.Count!=1))return Failure(400,"PARTNER_INVALID","Tham số tra cứu không hợp lệ.");
            return Ok(new{success=true,data=await service.GetListAsync(new(searchTerm,entityType,isActive,pageNumber,pageSize,includeDeleted),ct)});
        });
    }
    [HttpGet("reference")]
    public Task<IActionResult> Reference(string? searchTerm=null,int pageNumber=1,int pageSize=20,CancellationToken ct=default)=>
        Run(async()=>Ok(new{success=true,data=await service.GetListAsync(new(searchTerm,null,true,pageNumber,pageSize),ct)}));
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id,CancellationToken ct=default)=>Run(async()=>{
        var p=await service.GetByIdAsync(id,ct);return p is null?Failure(404,"PARTNER_NOT_FOUND","Không tìm thấy đơn vị."):Ok(new{success=true,data=p});
    });
    [HttpPost,Authorize(Policy="CatalogManage")]
    public Task<IActionResult> Create(CreatePartnerRequest request,CancellationToken ct=default)=>Run(async()=>{
        var p=await service.CreateAsync(request,Actor(),ct);return CreatedAtAction(nameof(Detail),new{id=p.Id},new{success=true,data=p});
    });
    [HttpPut("{id:guid}"),Authorize(Policy="CatalogManage")]
    public Task<IActionResult> Update(Guid id,UpdatePartnerRequest request,CancellationToken ct=default)=>Run(async()=>Ok(new{success=true,data=await service.UpdateAsync(id,request,Actor(),ct)}));
    [HttpDelete("{id:guid}"),Authorize(Policy="CatalogManage")]
    public Task<IActionResult> Delete(Guid id,PartnerVersionRequest request,CancellationToken ct=default)=>Run(async()=>Ok(new{success=true,data=await service.ChangeDeletionAsync(id,true,request.ExpectedVersion,Actor(),ct)}));
    [HttpPost("{id:guid}/restore"),Authorize(Policy="CatalogManage")]
    public Task<IActionResult> Restore(Guid id,PartnerVersionRequest request,CancellationToken ct=default)=>Run(async()=>Ok(new{success=true,data=await service.ChangeDeletionAsync(id,false,request.ExpectedVersion,Actor(),ct)}));
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        Response.Headers.CacheControl="no-store";
        try{return await action();}
        catch(PartnerRuleException e){return Failure(e.Status,e.Code,e.Message);}
        catch(System.Data.Common.DbException){return Failure(503,"PARTNER_DEPENDENCY_UNAVAILABLE","Danh mục đơn vị đang không khả dụng.");}
        catch(Microsoft.EntityFrameworkCore.DbUpdateException){return Failure(503,"PARTNER_DEPENDENCY_UNAVAILABLE","Không thể lưu danh mục đơn vị.");}
    }
    private IActionResult Failure(int status,string code,string message)
    {Response.Headers.CacheControl="no-store";return StatusCode(status,new{success=false,data=(object?)null,message,errors=new[]{new{field="",code,message}},traceId=HttpContext.TraceIdentifier});}
}
