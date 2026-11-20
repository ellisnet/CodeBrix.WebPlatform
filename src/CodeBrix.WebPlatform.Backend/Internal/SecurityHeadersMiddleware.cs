using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Adds <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c> and <c>Referrer-Policy</c> to every
/// answer, and the application's <c>Content-Security-Policy</c> when it gave one. The headers are
/// set when the answer starts, so error answers written further down carry them too.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;

    public SecurityHeadersMiddleware(RequestDelegate next, string contentSecurityPolicy)
    {
        _next = next;
        _contentSecurityPolicy = contentSecurityPolicy;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;
        response.OnStarting(() =>
        {
            var headers = response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (_contentSecurityPolicy != null)
            {
                headers.ContentSecurityPolicy = _contentSecurityPolicy;
            }

            return Task.CompletedTask;
        });
        return _next(context);
    }
}
