using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Infrastructure;
using WorkSite.Api.Validation;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Controllers;

/// <summary>The signed-in user's summary report and CSV export.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : WorkSiteControllerBase
{
    private readonly IReportRepository _reports;

    /// <summary>Creates the controller.</summary>
    /// <param name="reports">Computes the reports.</param>
    public ReportsController(IReportRepository reports)
    {
        _reports = reports;
    }

    /// <summary>Totals and grouped rows over a date range.</summary>
    /// <param name="from">The first work date, inclusive; the first of the current month when absent.</param>
    /// <param name="to">The last work date, inclusive; the end of the current month when absent.</param>
    /// <param name="groupBy">client (default), project, day, week or month.</param>
    /// <param name="clientId">Only this client's entries.</param>
    /// <param name="projectId">Only this project's entries.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the summary; 400 for invalid parameters.</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery, WireOptional] string groupBy, [FromQuery] Guid? clientId, [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var (start, end) = Range(errors, from, to);
        var grouping = ReportGroupBy.Client;
        if (!string.IsNullOrWhiteSpace(groupBy)
            && (!Enum.TryParse(groupBy.Trim(), ignoreCase: true, out grouping) || int.TryParse(groupBy, out _)))
        {
            errors.Add("groupBy", "Group by client, project, day, week or month.");
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var summary = await _reports.GetSummaryAsync(CurrentUserId, new ReportQuery
        {
            From = start,
            To = end,
            GroupBy = grouping,
            ClientId = clientId,
            ProjectId = projectId,
        }, cancellationToken);
        return Ok(summary);
    }

    /// <summary>Every entry in a date range as a CSV download.</summary>
    /// <param name="from">The first work date, inclusive; the first of the current month when absent.</param>
    /// <param name="to">The last work date, inclusive; the end of the current month when absent.</param>
    /// <param name="clientId">Only this client's entries.</param>
    /// <param name="projectId">Only this project's entries.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 <c>text/csv</c> as an attachment; 400 for invalid parameters.</returns>
    [HttpGet("export.csv")]
    [WireFile]
    public async Task<IActionResult> Export([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? clientId, [FromQuery] Guid? projectId, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var (start, end) = Range(errors, from, to);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var rows = await _reports.GetExportRowsAsync(CurrentUserId, start, end, clientId, projectId, cancellationToken);
        Response.Headers.ContentDisposition = $"attachment; filename=\"{CsvExport.FileName(start, end)}\"";
        return Content(CsvExport.Build(rows), "text/csv; charset=utf-8");
    }

    private static (DateOnly From, DateOnly To) Range(ValidationErrors errors, DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? ReportPeriods.StartOfMonth(today);
        var end = to ?? ReportPeriods.EndOfMonth(today);
        if (start > end)
        {
            errors.Add("to", "The end date must be on or after the start date.");
        }

        return (start, end);
    }
}
