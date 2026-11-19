using System;
using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>A summary report: totals and grouped rows.</summary>
[WireRequired]
public sealed class ReportSummary
{
    /// <summary>The first work date, inclusive.</summary>
    public DateOnly From { get; set; }

    /// <summary>The last work date, inclusive.</summary>
    public DateOnly To { get; set; }

    /// <summary>The grouping: client, project, day, week or month.</summary>
    public string GroupBy { get; set; } = string.Empty;

    /// <summary>All hours in the report.</summary>
    public decimal TotalHours { get; set; }

    /// <summary>Billable hours.</summary>
    public decimal BillableHours { get; set; }

    /// <summary>Non-billable hours.</summary>
    public decimal NonBillableHours { get; set; }

    /// <summary>The amount of the billable hours at their projects' rates.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>The number of entries.</summary>
    public int EntryCount { get; set; }

    /// <summary>The grouped rows.</summary>
    public IReadOnlyList<ReportRow> Rows { get; set; } = [];
}
