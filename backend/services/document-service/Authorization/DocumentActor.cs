using System.Security.Claims;

namespace DocumentService;

public sealed record DocumentActor(Guid UserId, Guid? DepartmentId, IReadOnlySet<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role);

    public static DocumentActor FromPrincipal(ClaimsPrincipal principal)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");

        var departmentValue = principal.FindFirstValue("departmentId");
        var departmentId = Guid.TryParse(departmentValue, out var parsedDepartmentId) && parsedDepartmentId != Guid.Empty
            ? parsedDepartmentId
            : (Guid?)null;

        var roles = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("role"))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        return new DocumentActor(userId, departmentId, roles);
    }
}
