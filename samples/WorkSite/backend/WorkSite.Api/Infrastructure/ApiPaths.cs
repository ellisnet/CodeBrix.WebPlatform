using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>Tells API requests from static-file requests.</summary>
public static class ApiPaths
{
    /// <summary>The prefix of every API route.</summary>
    public static readonly PathString Prefix = new("/api");

    /// <summary>Whether the request is for an API route.</summary>
    /// <param name="request">The request.</param>
    /// <returns>True for <c>/api</c> and everything below it.</returns>
    public static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments(Prefix);
}
