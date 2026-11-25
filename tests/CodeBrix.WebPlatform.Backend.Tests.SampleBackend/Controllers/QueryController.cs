using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;

/// <summary>What the query controller saw of its query value.</summary>
[WireRequired]
public sealed class QueryValueResponse
{
    /// <summary>True when the bound parameter was null.</summary>
    public bool IsNull { get; set; }

    /// <summary>The bound value; null when it was null.</summary>
    [WireOptional]
    public string Value { get; set; }
}

/// <summary>
/// A required query-string value declared as the platform's samples declare one
/// (<c>[FromQuery, WireRequired] string</c>, nullable annotations off), answering what model
/// binding made of it.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/query")]
public sealed class QueryController : ControllerBase
{
    /// <summary>Answers what <paramref name="name"/> was bound to.</summary>
    /// <param name="name">The query value.</param>
    /// <returns>Whether it was null, and its value.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(QueryValueResponse), StatusCodes.Status200OK)]
    public ActionResult<QueryValueResponse> Get([FromQuery, WireRequired] string name)
        => new QueryValueResponse { IsNull = name == null, Value = name };
}
