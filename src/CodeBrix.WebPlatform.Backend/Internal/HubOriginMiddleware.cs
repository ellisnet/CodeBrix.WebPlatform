using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Cross-site protection for the hubs. A browser sends cookies with a WebSocket handshake to any
/// site and does not apply the same-origin policy to it, so every request below
/// <c>/api/hubs</c> (the negotiate call and the handshake alike) must carry an <c>Origin</c>
/// header naming this site. Anything else is answered 403 <c>forbidden_origin</c>. Public hubs
/// are covered too.
/// </summary>
internal sealed class HubOriginMiddleware
{
    private readonly RequestDelegate _next;

    public HubOriginMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (ApiPaths.IsHub(request.Path) && !IsOwnOrigin(request))
        {
            return ApiProblemWriter.WriteAsync(context, ApiProblems.ForbiddenOrigin());
        }

        return _next(context);
    }

    /// <summary>True when the request has exactly one <c>Origin</c> header, equal to this site's scheme and host.</summary>
    internal static bool IsOwnOrigin(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var origins = request.Headers.Origin;
        if (origins.Count != 1 || string.IsNullOrEmpty(origins[0]) || !request.Host.HasValue)
        {
            return false;
        }

        var own = request.Scheme + "://" + request.Host.Value;
        return string.Equals(origins[0], own, StringComparison.OrdinalIgnoreCase);
    }
}
