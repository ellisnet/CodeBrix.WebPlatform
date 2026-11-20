using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// The error bodies that are WorkSite's own. The general ones (validation, not found, conflict,
/// rate limited, ...) are the platform backend's <c>ApiProblems</c>.
/// </summary>
public static class WorkSiteProblems
{
    /// <summary>401 <c>invalid_credentials</c>.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem InvalidCredentials() => new()
    {
        Title = "The username or password is incorrect.",
        Status = StatusCodes.Status401Unauthorized,
        Code = ApiProblemCodes.InvalidCredentials,
    };
}
