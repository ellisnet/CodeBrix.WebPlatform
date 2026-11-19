using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Computes one user's reports.</summary>
public interface IReportRepository
{
    /// <summary>Totals and grouped rows over a date range.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="query">The range, grouping and filters.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The summary.</returns>
    Task<ReportSummary> GetSummaryAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every entry in a date range as an export row, oldest first.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="from">The first work date, inclusive.</param>
    /// <param name="to">The last work date, inclusive.</param>
    /// <param name="clientId">Only this client's entries, or null.</param>
    /// <param name="projectId">Only this project's entries, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ExportRow>> GetExportRowsAsync(Guid userId, DateOnly from, DateOnly to, Guid? clientId,
        Guid? projectId, CancellationToken cancellationToken = default);
}
