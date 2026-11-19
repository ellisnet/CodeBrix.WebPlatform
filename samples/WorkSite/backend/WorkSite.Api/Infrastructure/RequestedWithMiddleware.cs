using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Cross-site request protection: every POST, PUT, PATCH or DELETE to the API must carry
/// <c>X-Requested-With: WorkSite</c>, which a cross-site form or a simple cross-origin request
/// cannot add. Requests without it get 400 <c>missing_request_header</c>. The value is the one
/// the backend declares with <c>[assembly: WireRequestMarker]</c> (WireDeclarations.cs), which the
/// generated frontend code sends, so there is one source for it.
/// </summary>
public sealed class RequestedWithMiddleware
{
    /// <summary>The header name.</summary>
    public const string HeaderName = WireRequestMarkerAttribute.HeaderName;

    /// <summary>The required header value: the backend's declared request marker.</summary>
    public static readonly string HeaderValue = WireRequestMarkerAttribute.GetValue(typeof(RequestedWithMiddleware).Assembly);

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    public RequestedWithMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Rejects unmarked state-changing API requests; passes everything else on.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (ApiPaths.IsApi(request) && ChangesState(request.Method)
            && !string.Equals(request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            return ApiProblemWriter.WriteAsync(context, ApiProblems.MissingRequestHeader());
        }

        return _next(context);
    }

    private static bool ChangesState(string method)
        => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method)
           || HttpMethods.IsDelete(method);
}
