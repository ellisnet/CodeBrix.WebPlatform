using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Gives a contract error body to any API 4xx response the framework produced without one
/// (for example 415 for a non-JSON body). 401 stays empty, as the contract requires.
/// </summary>
public static class ApiStatusCodePages
{
    /// <summary>The handler passed to <c>UseStatusCodePages</c>.</summary>
    /// <param name="context">The status code context.</param>
    /// <returns>A task that completes when any body is written.</returns>
    public static Task HandleAsync(StatusCodeContext context)
    {
        var http = context.HttpContext;
        var status = http.Response.StatusCode;
        if (!ApiPaths.IsApi(http.Request) || status == StatusCodes.Status401Unauthorized || status >= 500)
        {
            return Task.CompletedTask;
        }

        return ApiProblemWriter.WriteAsync(http, ApiProblems.ForStatus(status));
    }
}
