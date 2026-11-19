using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WorkSite.Core.Contracts;
using WorkSite.DataAccess.Schema;

namespace WorkSite.Api.Controllers;

/// <summary>Liveness check for load balancers and Docker health checks.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
[WireIgnore]
public sealed class HealthController : WorkSiteControllerBase
{
    private readonly SchemaVersionReader _schemaVersion;
    private readonly ILogger<HealthController> _logger;

    /// <summary>Creates the controller.</summary>
    /// <param name="schemaVersion">Reads the schema version.</param>
    /// <param name="logger">Where database failures are logged.</param>
    public HealthController(SchemaVersionReader schemaVersion, ILogger<HealthController> logger)
    {
        _schemaVersion = schemaVersion;
        _logger = logger;
    }

    /// <summary>Reports whether the database answers, and its schema version.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 <c>{ status: "ok", databaseVersion }</c>; 503 when the database does not answer.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var version = await _schemaVersion.GetCurrentVersionAsync(cancellationToken);
            return Ok(new HealthResponse { Status = "ok", DatabaseVersion = version });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "The health check could not reach the database.");
            return StatusCode(503, new HealthResponse { Status = "unavailable", DatabaseVersion = 0 });
        }
    }
}
