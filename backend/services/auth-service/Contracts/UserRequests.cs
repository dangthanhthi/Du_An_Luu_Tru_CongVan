using System.ComponentModel.DataAnnotations;

namespace AuthService;

public sealed record CreateUserRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Username,
    [Required, StringLength(200, MinimumLength = 8)] string Password,
    [Required, StringLength(200)] string FullName,
    [EmailAddress, StringLength(200)] string? Email,
    [StringLength(20)] string? Phone,
    Guid? DepartmentId,
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateUserRequest(
    [Required, StringLength(200)] string FullName,
    [EmailAddress, StringLength(200)] string? Email,
    [StringLength(20)] string? Phone,
    Guid? DepartmentId,
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateUserStatusRequest(bool IsActive);
