using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;

/// <summary>
/// Controllers OUTSIDE /api, at addresses a frontend application's fallback would also answer:
/// the tests prove that the controller wins.
/// </summary>
[ApiController]
[AllowAnonymous]
[WireIgnore]
public sealed class SiteStatusController : ControllerBase
{
    /// <summary>The text a controller outside /api answers with.</summary>
    public const string Answer = "site status from a controller";

    /// <summary>At the site root's level, where the default application's fallback also answers.</summary>
    /// <returns>The text.</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ContentResult Status() => Content(Answer, "text/plain");

    /// <summary>Below the named application "sales", where its fallback also answers.</summary>
    /// <returns>The text.</returns>
    [HttpGet("sales/status")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ContentResult SalesStatus() => Content(Answer, "text/plain");

    /// <summary>At the address of a file the web root may hold too.</summary>
    /// <returns>The text.</returns>
    [HttpGet("robots.txt")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ContentResult Robots() => Content(Answer, "text/plain");
}
