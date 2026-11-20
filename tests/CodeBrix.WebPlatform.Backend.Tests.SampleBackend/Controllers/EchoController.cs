using System;
using System.ComponentModel.DataAnnotations;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;

/// <summary>What the echo controller takes.</summary>
[WireRequired]
public sealed class EchoRequest
{
    /// <summary>The text to answer with.</summary>
    [Required]
    public string Text { get; set; }

    /// <summary>How many times.</summary>
    public int Count { get; set; }
}

/// <summary>What the echo controller answers.</summary>
[WireRequired]
public sealed class EchoResponse
{
    /// <summary>The text.</summary>
    public string Text { get; set; }

    /// <summary>How many times.</summary>
    public int Count { get; set; }
}

/// <summary>A public controller with the answers the platform's plumbing is about.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/echo")]
public sealed class EchoController : ControllerBase
{
    /// <summary>A plain answer.</summary>
    /// <param name="count">How many times.</param>
    /// <returns>The answer.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(EchoResponse), StatusCodes.Status200OK)]
    public ActionResult<EchoResponse> Get([FromQuery] int count = 1) => new EchoResponse { Text = "echo", Count = count };

    /// <summary>Answers the body it was sent.</summary>
    /// <param name="payload">The body.</param>
    /// <returns>The answer.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EchoResponse), StatusCodes.Status200OK)]
    public ActionResult<EchoResponse> Post(EchoRequest payload) => new EchoResponse { Text = payload.Text, Count = payload.Count };

    /// <summary>Answers by id; the route takes only a guid.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The answer.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EchoResponse), StatusCodes.Status200OK)]
    public ActionResult<EchoResponse> ById(Guid id) => new EchoResponse { Text = id.ToString(), Count = 1 };

    /// <summary>A bare 404, as a controller that writes no body of its own gives.</summary>
    /// <returns>404 without a body.</returns>
    [HttpGet("missing")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Missing() => NotFound();

    /// <summary>A bare 409.</summary>
    /// <returns>409 without a body.</returns>
    [HttpPut("conflict")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Conflicting() => Conflict();

    /// <summary>A bare 503, which the platform leaves alone.</summary>
    /// <returns>503 without a body.</returns>
    [HttpGet("unavailable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Unavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable);

    /// <summary>A failure with a body of the application's own, which the platform leaves alone.</summary>
    /// <returns>400 with the application's body.</returns>
    [HttpDelete("own-problem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult OwnProblem() => BadRequest(new { title = "Own.", status = 400, code = "own_code" });
}
