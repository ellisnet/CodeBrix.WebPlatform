using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Gives the standard error body to every 4xx answer below <c>/api</c> that the framework produced
/// without a body. A 401 stays empty (the platform's "not signed in"), and answers outside
/// <c>/api</c> and 5xx answers are left alone.
/// </summary>
internal static class ApiStatusCodePages
{
    /// <summary>The handler passed to <c>UseStatusCodePages</c>.</summary>
    public static Task HandleAsync(StatusCodeContext context)
    {
        var http = context.HttpContext;
        var status = http.Response.StatusCode;
        if (!ApiPaths.IsApi(http.Request.Path) || status == StatusCodes.Status401Unauthorized || status < 400 || status >= 500)
        {
            return Task.CompletedTask;
        }

        return ApiProblemWriter.WriteAsync(http, ApiProblems.ForStatus(status));
    }
}
