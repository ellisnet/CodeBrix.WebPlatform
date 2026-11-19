using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Controllers;

/// <summary>The signed-in user's dashboard figures.</summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : WorkSiteControllerBase
{
    private readonly IDashboardRepository _dashboard;

    /// <summary>Creates the controller.</summary>
    /// <param name="dashboard">Computes the figures.</param>
    public DashboardController(IDashboardRepository dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>The dashboard as seen on <paramref name="today"/>.</summary>
    /// <param name="today">The browser's local date; the server's UTC date when absent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the dashboard.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Dashboard), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] DateOnly? today, CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAsync(CurrentUserId, today ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken));
}
