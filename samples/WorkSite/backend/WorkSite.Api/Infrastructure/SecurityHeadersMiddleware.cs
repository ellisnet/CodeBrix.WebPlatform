using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WorkSite.Api.Configuration;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Adds <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c>, <c>Referrer-Policy</c> and the
/// configured <c>Content-Security-Policy</c> to every response.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="settings">The settings holding the Content-Security-Policy.</param>
    public SecurityHeadersMiddleware(RequestDelegate next, WorkSiteSettings settings)
    {
        _next = next;
        _contentSecurityPolicy = settings.Security.ContentSecurityPolicy;
    }

    /// <summary>Registers the headers and runs the rest of the pipeline.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = _contentSecurityPolicy;
            return Task.CompletedTask;
        });
        return _next(context);
    }
}
