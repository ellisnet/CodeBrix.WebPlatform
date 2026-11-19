using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Report generation progress: the frontend asks for a report and reads a stream of progress
/// steps over a few seconds. The work is made up; cancelling the stream stops it. Needs sign-in.
/// </summary>
[WireHub("/api/hubs/report-progress")]
[Authorize]
[WireRequired]
public sealed class ReportProgressHub : Hub<IReportProgressClient>
{
    private static readonly string[] Stages =
    [
        "Collecting time entries", "Collecting time entries", "Grouping by client", "Grouping by project",
        "Adding up hours", "Adding up amounts", "Checking totals", "Laying out the pages", "Writing the file", "Done",
    ];

    private readonly TimeProvider _time;

    /// <summary>Creates the hub.</summary>
    /// <param name="time">The clock.</param>
    public ReportProgressHub(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Generates a report for a date range, streaming its progress.</summary>
    /// <param name="from">The first day of the report.</param>
    /// <param name="to">The last day of the report.</param>
    /// <param name="cancellationToken">Cancelled when the frontend drops the stream.</param>
    /// <returns>Ten steps, about 400 ms apart, the last one complete.</returns>
    /// <exception cref="HubException">The range ends before it starts.</exception>
    public IAsyncEnumerable<ReportProgress> Generate(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new HubException("The end date must be on or after the start date.");
        }

        return Steps(cancellationToken);
    }

    private async IAsyncEnumerable<ReportProgress> Steps([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < Stages.Length; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(400), _time, cancellationToken);
            yield return new ReportProgress
            {
                Percent = (i + 1) * 100 / Stages.Length,
                Stage = Stages[i],
                IsComplete = i == Stages.Length - 1,
            };
        }
    }
}
