using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Cross-site request protection: every POST, PUT, PATCH or DELETE below <c>/api</c> must carry
/// the backend's request marker in <c>X-Requested-With</c>, which a cross-site form or a simple
/// cross-origin request cannot add. Anything else is answered 400 <c>missing_request_header</c>.
/// </summary>
internal sealed class RequestMarkerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _marker;

    public RequestMarkerMiddleware(RequestDelegate next, string marker)
    {
        _next = next;
        _marker = marker;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (ApiPaths.IsApi(request.Path) && ApiPaths.ChangesState(request.Method) && !HasMarker(request, _marker))
        {
            return ApiProblemWriter.WriteAsync(context, ApiProblems.MissingRequestHeader());
        }

        return _next(context);
    }

    /// <summary>True when the request carries exactly one <c>X-Requested-With</c> value, equal to the marker.</summary>
    internal static bool HasMarker(HttpRequest request, string marker)
    {
        var values = request.Headers[WireRequestMarkerAttribute.HeaderName];
        return values.Count == 1 && string.Equals(values[0], marker, StringComparison.Ordinal);
    }
}
