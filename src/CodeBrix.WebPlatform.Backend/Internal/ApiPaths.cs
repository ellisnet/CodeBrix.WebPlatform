using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>Tells API and hub requests from the rest.</summary>
internal static class ApiPaths
{
    public static readonly PathString Api = new(PlatformBackendPaths.Api);

    public static readonly PathString Hubs = new(PlatformBackendPaths.Hubs);

    /// <summary>True for <c>/api</c> and everything below it (segment by segment, any casing).</summary>
    public static bool IsApi(PathString path) => path.StartsWithSegments(Api);

    /// <summary>True for <c>/api/hubs</c> and everything below it (segment by segment, any casing).</summary>
    public static bool IsHub(PathString path) => path.StartsWithSegments(Hubs);

    /// <summary>True for the methods that change state: POST, PUT, PATCH and DELETE.</summary>
    public static bool ChangesState(string method)
        => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method)
           || HttpMethods.IsDelete(method);
}
