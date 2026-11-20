using System;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Problems;

/// <summary>
/// Writes an <see cref="ApiProblem"/> as the answer from middleware or an endpoint delegate,
/// outside MVC. (In a controller, return an <see cref="ApiProblemResult"/>.)
/// </summary>
public static class ApiProblemWriter
{
    /// <summary>
    /// Sets the status code to <see cref="ApiProblem.Status"/> and writes the problem as
    /// <c>application/problem+json</c> with camelCase names.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="problem">The problem.</param>
    /// <returns>A task that completes when the body is written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="problem"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The answer has already started, so its status
    /// can no longer be set; write the problem before anything else is written.</exception>
    public static async Task WriteAsync(HttpContext context, ApiProblem problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The answer has already started, so an ApiProblem can no longer be written; write the problem before anything else is written.");
        }

        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = ApiProblem.ContentType;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonSerializerOptions.Web,
            context.RequestAborted);
    }
}
