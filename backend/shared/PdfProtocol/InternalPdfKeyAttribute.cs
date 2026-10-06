using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Das.PdfProtocol;

// Separate explicitly provisioned machine credential. Never derive from JWT/user claims.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalPdfKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!context.HttpContext.Request.IsHttps && !context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()) {
            context.Result = new StatusCodeResult(503); return Task.CompletedTask;
        }
        var expected = config["PdfProtocol:Key"];
        var values = context.HttpContext.Request.Headers["X-DAS-Pdf-Key"];
        var supplied = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(expected) || Encoding.UTF8.GetByteCount(expected) < 32 || expected.Length > 1024)
            context.Result = new StatusCodeResult(503);
        else if (string.IsNullOrEmpty(supplied) || supplied.Length > 1024 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            context.Result = new UnauthorizedResult();
        return Task.CompletedTask;
    }
}
