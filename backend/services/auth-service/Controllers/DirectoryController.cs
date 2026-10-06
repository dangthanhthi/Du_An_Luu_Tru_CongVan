using AuthService.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService;

[ApiController]
[Route("api/v2/directory")]
[Authorize]
public sealed class DirectoryController(OrganizationDirectoryStore store, IConfiguration configuration) : ControllerBase
{
    [HttpGet("departments")]
    public Task<IActionResult> Departments(bool includeGroups = false, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(pageNumber, pageSize)) return Task.FromResult<IActionResult>(
            BadRequest(ApiResponse<object>.Fail("Invalid paging.", "INVALID_PAGING")));
        return ReadAsync(directory =>
        {
            var scope = Scope(directory.Projection);
            var rows = directory.Projection.Units.Where(x => x.IsActive && (includeGroups || x.IsDepartment) &&
                (HasReadAll || scope.Contains(x.IsDepartment ? x.Id : x.ParentId!.Value)))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
            return Page(rows, pageNumber, pageSize, directory);
        }, ct);
    }
    [HttpGet("users")]
    public Task<IActionResult> Users(Guid departmentId, string purpose = "originator", int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(pageNumber, pageSize) || departmentId == Guid.Empty || purpose != "originator")
            return Task.FromResult<IActionResult>(BadRequest(ApiResponse<object>.Fail("Invalid lookup.", "INVALID_LOOKUP")));
        return ReadAsync(directory =>
        {
            if (!directory.Projection.Units.Any(x => x.Id == departmentId && x.IsActive && x.IsDepartment))
                return StatusCode(404, ApiResponse<object>.Fail("Department not found."));
            if (!HasReadAll && !Scope(directory.Projection).Contains(departmentId))
                return StatusCode(403, ApiResponse<object>.Fail("Department is outside your scope."));
            var rows = directory.Projection.Users.Where(x => x.IsActive &&
                directory.Projection.GetActiveDepartmentIds(x.Id).Contains(departmentId))
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id)
                .Select(x => new DirectoryUserLookup(x.Id, x.DisplayName)).ToArray();
            return Page(rows, pageNumber, pageSize, directory);
        }, ct);
    }
    private bool HasReadAll => User.HasClaim("das_capability", "DirectoryReadAll");
    private IReadOnlyList<Guid> Scope(PreparedOrganizationDirectory projection) =>
        projection.GetActiveDepartmentIds(Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value));
    private async Task<IActionResult> ReadAsync(Func<PersistedDirectory, IActionResult> query, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var subject = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (User.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var actorId) || actorId == Guid.Empty)
            return StatusCode(401, ApiResponse<object>.Fail("Authentication is required."));
        try
        {
            var directory = await store.ReadFreshAsync(configuration["Directory:SourceId"] ?? "eap-organization", TimeSpan.FromSeconds(60), ct);
            if (!directory.Projection.Users.Any(x => x.Id == actorId && x.IsActive))
                return StatusCode(403, ApiResponse<object>.Fail("No active directory identity."));
            return query(directory);
        }
        catch (DirectoryUnavailableException e)
        {
            return StatusCode(503, ApiResponse<object>.Fail("Organization data is unavailable or awaiting verification.", e.Code));
        }
        catch (System.Data.Common.DbException)
        {
            return StatusCode(503, ApiResponse<object>.Fail("Organization data is unavailable.", "DIRECTORY_DEPENDENCY_UNAVAILABLE"));
        }
    }
    private IActionResult Page<T>(T[] rows, int number, int size, PersistedDirectory directory)
    {
        var offset = (long)(number - 1) * size;
        return Ok(ApiResponse<DirectoryPage<T>>.Ok(new(
            offset >= rows.Length ? [] : rows.Skip((int)offset).Take(size).ToArray(), number, size, rows.Length,
            directory.AuthorizationRevision, directory.VerifiedAt)));
    }
    private static bool ValidPage(int number, int size) => number >= 1 && size is >= 1 and <= 100;
}
public sealed record DirectoryPage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount,
    long AuthorizationRevision, DateTimeOffset VerifiedAt);
public sealed record DirectoryUserLookup(Guid Id, string DisplayName);

