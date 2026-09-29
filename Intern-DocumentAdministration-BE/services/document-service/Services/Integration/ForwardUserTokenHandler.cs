using System.Net.Http.Headers;

namespace DocumentService;

public sealed class ForwardUserTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(value, out var header))
            request.Headers.Authorization = header;

        return base.SendAsync(request, cancellationToken);
    }
}
