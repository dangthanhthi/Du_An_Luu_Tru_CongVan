using Microsoft.AspNetCore.Mvc;

namespace AuthService;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, IReadOnlyList<string> Errors)
{
    public static ApiResponse<T> Ok(T? data, string? message = null) =>
        new(true, data, message, []);

    public static ApiResponse<T> Fail(string message, params string[] errors) =>
        new(false, default, message, errors);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class ApiResults
{
    public static IActionResult Error(ControllerBase controller, int statusCode, string message, params string[] errors) =>
        controller.StatusCode(statusCode, ApiResponse<object>.Fail(message, errors));
}
