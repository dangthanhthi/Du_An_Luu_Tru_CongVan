using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PartnerService;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception,
            "[partner-service] Unhandled exception: {Message}", exception.Message);

        bool isAuthError = exception is UnauthorizedAccessException;
        bool isForbiddenError = isAuthError && (
            exception.Message.Contains("khong co quyen", StringComparison.OrdinalIgnoreCase) || 
            exception.Message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) || 
            exception.Message.Contains("vai tro", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("vai trò", StringComparison.OrdinalIgnoreCase));

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException when isForbiddenError => (403, exception.Message),
            UnauthorizedAccessException                       => (401, exception.Message),
            KeyNotFoundException                              => (404, exception.Message),
            ArgumentException                                 => (400, exception.Message),
            InvalidOperationException                         => (400, exception.Message),
            DbUpdateException                                 => (400, "Du lieu bi trung lap hoac vi pham rang buoc co so du lieu."),
            _                                                 => (500, "Da xay ra loi he thong. Vui long lien he quan tri vien (Ma loi: PARTNER_SVC_500).")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(
            new { success = false, message },
            cancellationToken);

        return true;
    }
}
