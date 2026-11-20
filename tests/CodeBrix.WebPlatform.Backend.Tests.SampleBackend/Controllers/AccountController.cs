using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;

/// <summary>A controller that needs sign-in.</summary>
[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    /// <summary>The signed-in user's name.</summary>
    /// <returns>The name.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult<string> Get() => User.Identity?.Name;

    /// <summary>A change that needs sign-in.</summary>
    /// <returns>204.</returns>
    [HttpPost("touch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Touch() => NoContent();
}
