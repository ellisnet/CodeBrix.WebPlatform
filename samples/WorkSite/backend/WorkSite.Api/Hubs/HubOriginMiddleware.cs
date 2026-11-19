using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;
using WorkSite.Api.Infrastructure;
using WorkSite.Core.Contracts;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Cross-site protection for the hubs. A browser sends cookies with a WebSocket handshake to any
/// site and does not apply the same-origin policy to it, so every request to a hub path (the
/// negotiate call and the handshake alike) must carry an <c>Origin</c> header naming this site.
/// Anything else gets 403 <c>forbidden_origin</c>. This applies to the public hubs too. (The
/// negotiate call, being a POST under <c>/api</c>, must also carry <c>X-Requested-With</c>; see
/// <see cref="RequestedWithMiddleware"/>.)
/// </summary>
public sealed class HubOriginMiddleware
{
    /// <summary>
    /// The path every hub is served under. It is inside <c>/api</c> because the session cookie is
    /// only sent to <c>/api</c> paths; a hub outside it would never see who is signed in.
    /// </summary>
    public static readonly PathString Prefix = new("/api/hubs");

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    public HubOriginMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Refuses hub requests from other sites; passes everything else on.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments(Prefix) && !IsOwnOrigin(request))
        {
            return ApiProblemWriter.WriteAsync(context, new ApiProblem
            {
                Title = "The connection was refused because it did not come from the WorkSite site.",
                Status = StatusCodes.Status403Forbidden,
                Code = WorkSiteProblemCodes.ForbiddenOrigin,
            });
        }

        return _next(context);
    }

    /// <summary>True when the request's <c>Origin</c> header is exactly this site's scheme and host.</summary>
    /// <param name="request">The request.</param>
    /// <returns>Whether the request came from a page of this site.</returns>
    public static bool IsOwnOrigin(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var origins = request.Headers.Origin;
        if (origins.Count != 1 || string.IsNullOrEmpty(origins[0]))
        {
            return false;
        }

        var own = request.Scheme + "://" + request.Host.Value;
        return string.Equals(origins[0], own, StringComparison.OrdinalIgnoreCase);
    }
}
