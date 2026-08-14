using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentService;

/// <summary>
/// Exception được ném khi một service phụ thuộc (partner-service, files-service, notification-service)
/// không thể kết nối hoặc trả về lỗi. Sẽ được ánh xạ sang HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException : Exception
{
    public string ServiceName { get; }

    public ServiceUnavailableException(string serviceName, string message)
        : base($"[{serviceName}] {message}")
    {
        ServiceName = serviceName;
    }

    public ServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base($"[{serviceName}] {message}", innerException)
    {
        ServiceName = serviceName;
    }
}

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
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        bool is503Error = exception.Message.Contains("[503_SERVICE_UNAVAILABLE]")
            || exception is ServiceUnavailableException;

        bool isAuthError = exception is UnauthorizedAccessException;
        bool isForbiddenError = isAuthError && (
            exception.Message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) || 
            exception.Message.Contains("khong co quyen", StringComparison.OrdinalIgnoreCase) || 
            exception.Message.Contains("vai trò", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("vai tro", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("không thuộc phòng ban", StringComparison.OrdinalIgnoreCase));

        var statusCode = exception switch
        {
            _ when is503Error                                 => StatusCodes.Status503ServiceUnavailable,
            KeyNotFoundException                              => StatusCodes.Status404NotFound,
            UnauthorizedAccessException when isForbiddenError => StatusCodes.Status403Forbidden,
            UnauthorizedAccessException                       => StatusCodes.Status401Unauthorized,
            ArgumentException                                 => StatusCodes.Status400BadRequest,
            InvalidOperationException                         => StatusCodes.Status400BadRequest,
            Microsoft.EntityFrameworkCore.DbUpdateException   => StatusCodes.Status400BadRequest,
            _                                                 => StatusCodes.Status500InternalServerError
        };


        var response = new
        {
            success = false,
            message = exception.Message,
            errors = new string[] { }
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response), cancellationToken);

        return true;
    }
}
