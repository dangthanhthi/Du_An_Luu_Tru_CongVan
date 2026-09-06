using System.ComponentModel.DataAnnotations;

namespace AuthService;

public sealed record CreateRoleRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(300)] string? Description);
