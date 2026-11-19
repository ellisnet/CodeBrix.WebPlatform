using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BrokenBackend;

public sealed class Unmarked
{
    public string Name { get; set; }

    public object Anything { get; set; }

    public Unmarked Self { get; set; }
}

public sealed class OtherShape
{
    public int Value { get; set; }
}

[Route("api/things")]
public sealed class ThingsController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult NoResponse() => throw new NotSupportedException();

    [HttpGet("a")]
    [ProducesResponseType(204)]
    public IActionResult NoAuth() => throw new NotSupportedException();

    [Authorize]
    public IActionResult NoRoute() => throw new NotSupportedException();

    [HttpGet("b/{id}")]
    [Authorize]
    [ProducesResponseType(204)]
    public IActionResult MissingRouteParameter() => throw new NotSupportedException();

    [HttpGet("c")]
    [Authorize]
    [ProducesResponseType(typeof(OtherShape), 200)]
    [ProducesResponseType(204)]
    public IActionResult TwoShapes() => throw new NotSupportedException();

    [HttpGet("d")]
    [Authorize]
    [ProducesResponseType(typeof(Unmarked), 200)]
    public IActionResult UnmarkedBody([FromQuery] string text) => throw new NotSupportedException();

    [HttpGet("e/{*rest}")]
    [Authorize]
    [ProducesResponseType(204)]
    public IActionResult CatchAll(string rest) => throw new NotSupportedException();

    [HttpPost("f")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(OtherShape), 400)]
    public IActionResult WrongErrorBody([FromHeader] string token) => throw new NotSupportedException();

    [HttpPost("g")]
    [WireFile]
    [Authorize]
    public IActionResult FileByPost() => throw new NotSupportedException();
}

public sealed class UntypedHub : Hub
{
    public Task Hello() => Task.CompletedTask;
}

public interface IReplyClient
{
    Task<int> Ask();
}

[WireHub("/hubs/reply")]
public sealed class ReplyHub : Hub<IReplyClient>
{
    public Task Send(string text, IAsyncEnumerable<int> numbers) => Task.CompletedTask;
}

//Read on its own by the tests (ControllerReader), because a [WireSessionRefresh] action in the
//mapped backend would hide the "no session-refresh action" problem this backend must produce.
[ApiController]
[Route("api/refresh-mistakes")]
[WireIgnore]
public sealed class RefreshMistakesController : ControllerBase
{
    [HttpGet("refresh")]
    [Authorize]
    [WireSessionRefresh]
    [ProducesResponseType(204)]
    public IActionResult RefreshByGet(int id) => throw new NotSupportedException();

    [HttpPost("again")]
    [AllowAnonymous]
    [WireSessionRefresh]
    [ProducesResponseType(204)]
    public IActionResult Again() => throw new NotSupportedException();
}
