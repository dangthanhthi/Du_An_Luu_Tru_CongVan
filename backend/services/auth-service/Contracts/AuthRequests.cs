using System.ComponentModel.DataAnnotations;

namespace AuthService;

public sealed record LoginRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(200)] string Password);

public sealed record RefreshRequest(
    [Required] string RefreshToken);
