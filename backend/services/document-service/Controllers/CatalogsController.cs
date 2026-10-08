using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

[ApiController]
[Route("api/v2/catalogs")]
[Authorize]
public sealed class CatalogsController(CatalogService service, DocumentDbContext db, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("/api/v2/admin/catalogs/options")]
    public Task<IActionResult> GetAdminOptions(CancellationToken ct = default) => Execute(async () =>
    {
        if (Actor() == Guid.Empty) return Failure(401, "ACTOR_REQUIRED", "Authentication is required.");
        if (Request.Query.Count != 0) return Failure(400, "INVALID_ADMIN_CATALOG_QUERY", "Admin catalog query is invalid.");
        var capability = await authorization.AuthorizeAsync(User, null, "CatalogManage");
        return Success(new { canManage = capability.Succeeded });
    });

    [HttpGet("/api/v2/admin/catalogs")]
    [Authorize(Policy = "CatalogManage")]
    public Task<IActionResult> GetAdminPage(CancellationToken ct = default) =>
        Execute(async () => Success(await service.GetAdminPageAsync(CatalogAdminQuery.Parse(Request.Query), Actor(), ct)));

    [HttpGet]
    public Task<IActionResult> Get(string? groups = null, CancellationToken ct = default) =>
        Execute(async () => Success(await service.GetAsync(groups, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct = default) => Execute(async () =>
    {
        // Historical selected values remain readable after deactivation.
        var entry = await db.BusinessCatalogEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return entry is null ? Failure(404, "CATALOG_NOT_FOUND", "Catalog entry not found.") : Success(CatalogService.ToDto(entry));
    });

    [HttpPost("/api/v2/admin/catalogs")]
    [Authorize(Policy = "CatalogManage")]
    public Task<IActionResult> Create(CreateCatalogRequest request, CancellationToken ct = default) =>
        Execute(async () => StatusCode(201, Envelope(await service.CreateAsync(request.Group, request.Code, request.Name, Actor(), ct))));

    [HttpPut("/api/v2/admin/catalogs/{id:guid}")]
    [Authorize(Policy = "CatalogManage")]
    public Task<IActionResult> Update(Guid id, CatalogUpdateRequest request, CancellationToken ct = default) =>
        Execute(async () => Success(await service.UpdateAsync(id, request, Actor(), ct)));

    private Guid Actor()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(subject, out var id) ? id : Guid.Empty;
    }
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        Response.Headers.CacheControl = "no-store";
        try { return await action(); }
        catch (CatalogRuleException e) { return Failure(e.Status, e.Code, e.Message); }
        catch (System.Data.Common.DbException) { return Failure(503, "CATALOG_DEPENDENCY_UNAVAILABLE", "Catalog service is unavailable."); }
        catch (Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException) { return Failure(503, "CATALOG_DEPENDENCY_UNAVAILABLE", "Catalog service is unavailable."); }
    }
    private object Envelope(object? data) => new { success = true, data, message = (string?)null, errors = Array.Empty<object>(), traceId = HttpContext.TraceIdentifier };
    private IActionResult Success(object data) => Ok(Envelope(data));
    private IActionResult Failure(int status, string code, string message) =>
        StatusCode(status, new { success = false, data = (object?)null, message,
            errors = new[] { new { field = "", code, message } }, traceId = HttpContext.TraceIdentifier });
}
public sealed record CreateCatalogRequest(string Group, string Code, string Name);

[ApiController]
[Route("api/v2/distribution-targets")]
[Authorize]
public sealed class DistributionTargetsController(DocumentDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string? search = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (pageNumber < 1 || pageSize is < 1 or > 100 || search?.Length > 200)
            return BadRequest(new { success = false, message = "Invalid lookup parameters." });
        var query = db.DistributionTargets.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search.Trim()));
        try
        {
            var total = await query.CountAsync(ct);
            var offset = (long)(pageNumber - 1) * pageSize;
            var items = offset >= total ? [] : await query.OrderBy(x => x.LegacyId).Skip((int)offset).Take(pageSize)
                .Select(x => new { x.Id, x.Name, x.Initial, x.MappingState, x.Version }).ToListAsync(ct);
            return Ok(new { success = true, data = new { items, totalCount = total, pageNumber, pageSize },
                message = (string?)null, errors = Array.Empty<object>(), traceId = HttpContext.TraceIdentifier });
        }
        catch (System.Data.Common.DbException)
        {
            return StatusCode(503, new { success = false, message = "Recipient lookup is unavailable." });
        }
    }
}

