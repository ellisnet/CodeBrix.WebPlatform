using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Infrastructure;

namespace WorkSite.Api.Controllers;

/// <summary>
/// Answers every unknown <c>/api</c> route with 404 and the contract's error body, so the
/// single-page-app fallback never serves index.html for an API call.
/// </summary>
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[WireIgnore]
public sealed class ApiNotFoundController : WorkSiteControllerBase
{
    /// <summary>The catch-all action.</summary>
    /// <returns>404 <c>not_found</c>.</returns>
    [Route("api/{**path}", Order = int.MaxValue)]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    public IActionResult NoSuchRoute() => ProblemResult(ApiProblems.NotFound("There is no such API route."));
}
