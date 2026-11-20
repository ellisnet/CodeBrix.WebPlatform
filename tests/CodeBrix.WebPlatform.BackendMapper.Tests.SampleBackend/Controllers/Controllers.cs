using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleBackend.Contracts;

namespace SampleBackend.Controllers;

public abstract class SampleControllerBase : ControllerBase
{
    [NonAction]
    public string Helper() => string.Empty;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class WidgetsController : SampleControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Page<Widget>), 200)]
    public Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery, WireOptional] string text,
        [FromQuery] Shade shade, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    [HttpGet("{id:guid}")]
    public Task<ActionResult<Widget>> Get(Guid id, [FromServices] IServiceProvider services)
        => throw new NotSupportedException();

    [HttpPost]
    [ProducesResponseType<Widget>(201)]
    [ProducesResponseType(typeof(ApiProblem), 400)]
    [ProducesResponseType(401)]
    public Task<IActionResult> Create([FromBody] WidgetRequest request, [FromQuery] bool notify)
        => throw new NotSupportedException();

    [HttpPut("{id:guid}/parts/{partNumber:int}")]
    [ProducesResponseType(typeof(Part), 200)]
    public IActionResult ReplacePart(Guid id, int partNumber, Part part) => throw new NotSupportedException();

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteAsync(Guid id) => throw new NotSupportedException();

    [HttpGet("search")]
    [AllowAnonymous]
    public Task<List<Widget>> Search([FromQuery] WidgetSearch search) => throw new NotSupportedException();

    [HttpGet("export.csv")]
    [WireFile]
    public IActionResult Export([FromQuery] DateOnly? from, [FromQuery, WireRequired] string label) => throw new NotSupportedException();

    [HttpGet("manual.pdf")]
    [WireFile]
    public IActionResult Manual() => throw new NotSupportedException();

    [HttpGet("{id:guid}/picture.png")]
    [WireFile]
    [AllowAnonymous]
    public IActionResult Picture(Guid id) => throw new NotSupportedException();

    [HttpGet("/status")]
    [AllowAnonymous]
    public ActionResult<Dictionary<string, string>> Status() => throw new NotSupportedException();

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(Widget), 200)]
    [WireIgnore]
    public IActionResult Patch(Guid id) => throw new NotSupportedException();
}

[ApiController]
[Route("api/{**path}")]
[WireIgnore]
public sealed class FallbackController : ControllerBase
{
    [HttpGet]
    public IActionResult Any() => throw new NotSupportedException();
}

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(204)]
    public IActionResult Check() => throw new NotSupportedException();
}

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    [HttpPost("refresh")]
    [AllowAnonymous]
    [WireSessionRefresh]
    [ProducesResponseType(typeof(Account), 200)]
    public IActionResult Refresh(CancellationToken cancellationToken) => throw new NotSupportedException();
}
