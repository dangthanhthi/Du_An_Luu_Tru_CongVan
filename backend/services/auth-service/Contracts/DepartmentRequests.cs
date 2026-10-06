using System.ComponentModel.DataAnnotations;

namespace AuthService;

public sealed record DepartmentRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(50)] string Code);
