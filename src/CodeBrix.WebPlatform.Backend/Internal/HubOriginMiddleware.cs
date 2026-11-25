using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Cross-site protection for the hubs. A browser sends cookies with a WebSocket handshake to any
/// site and does not apply the same-origin policy to it, so every request below
/// <c>/api/hubs</c> (the negotiate call and the handshake alike) must carry an <c>Origin</c>
/// header naming this site, or one of the origins of
/// <see cref="PlatformBackendOptions.AdditionalHubOrigins"/>. Anything else is answered 403
/// <c>forbidden_origin</c>. Public hubs are covered too.
/// </summary>
internal sealed class HubOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _additionalOrigins;

    /// <summary>The middleware.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="additionalOrigins">The other origins accepted, as <see cref="ParseOrigins"/> made them; null for none.</param>
    public HubOriginMiddleware(RequestDelegate next, HashSet<string> additionalOrigins = null)
    {
        _next = next;
        _additionalOrigins = additionalOrigins ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Refuses a hub request from another origin; hands everything else on.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (ApiPaths.IsHub(request.Path) && !IsOwnOrigin(request) && !IsAdditionalOrigin(request, _additionalOrigins))
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

    /// <summary>
    /// True when the request has exactly one <c>Origin</c> header and it is one of
    /// <paramref name="additionalOrigins"/> (compared without regard to case; the header as a
    /// browser writes an origin: no default port, no trailing slash).
    /// </summary>
    internal static bool IsAdditionalOrigin(HttpRequest request, HashSet<string> additionalOrigins)
    {
        ArgumentNullException.ThrowIfNull(request);
        var origins = request.Headers.Origin;
        return additionalOrigins != null && additionalOrigins.Count > 0
            && origins.Count == 1 && !string.IsNullOrEmpty(origins[0]) && additionalOrigins.Contains(origins[0]);
    }

    /// <summary>
    /// Reads <see cref="PlatformBackendOptions.AdditionalHubOrigins"/>: each entry an http or
    /// https origin (scheme, host, optional port; a trailing slash allowed), written as a
    /// browser writes it - scheme and host in lower case, no default port (80 for http, 443 for
    /// https), no trailing slash.
    /// </summary>
    /// <exception cref="InvalidOperationException">The list is null, or an entry is not such an
    /// origin; the message names the entry.</exception>
    internal static HashSet<string> ParseOrigins(IEnumerable<string> entries)
    {
        if (entries == null)
        {
            throw new InvalidOperationException("PlatformBackendOptions.AdditionalHubOrigins is null; change its entries instead of replacing it.");
        }

        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var text = entry?.Trim();
            if (string.IsNullOrEmpty(text)
                || text.IndexOfAny(new[] { '?', '#', '*', ' ' }) >= 0
                || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !text.Contains("://", StringComparison.Ordinal)
                || string.IsNullOrEmpty(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.AbsolutePath != "/"
                || text.Substring(text.IndexOf("://", StringComparison.Ordinal) + 3).TrimEnd('/').Contains('/'))
            {
                throw new InvalidOperationException(
                    $"PlatformBackendOptions.AdditionalHubOrigins has the entry \"{entry}\", which is not an origin: write the scheme (http or https), the host and, when it is not the default, the port - for example \"http://localhost:8080\" - with no path, query or wildcard.");
            }

            origins.Add(uri.Scheme + "://" + uri.Host.ToLowerInvariant() + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port));
        }

        return origins;
    }
}
