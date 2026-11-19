using System;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>Writes an <see cref="ApiProblem"/> from middleware, outside MVC.</summary>
public static class ApiProblemWriter
{
    /// <summary>Sets the status code and writes the problem as <c>application/problem+json</c>.</summary>
    /// <param name="context">The request.</param>
    /// <param name="problem">The problem.</param>
    /// <returns>A task that completes when the body is written.</returns>
    public static async Task WriteAsync(HttpContext context, ApiProblem problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);
        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = ApiProblem.ContentType;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonSerializerOptions.Web,
            context.RequestAborted);
    }
}
