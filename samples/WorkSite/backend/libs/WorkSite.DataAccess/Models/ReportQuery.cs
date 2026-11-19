using System;
using WorkSite.Core.Models;

namespace WorkSite.DataAccess.Models;

/// <summary>What a summary report covers.</summary>
public sealed class ReportQuery
{
    /// <summary>The first work date, inclusive.</summary>
    public DateOnly From { get; set; }

    /// <summary>The last work date, inclusive.</summary>
    public DateOnly To { get; set; }

    /// <summary>How rows are grouped.</summary>
    public ReportGroupBy GroupBy { get; set; }

    /// <summary>Only this client's entries.</summary>
    public Guid? ClientId { get; set; }

    /// <summary>Only this project's entries.</summary>
    public Guid? ProjectId { get; set; }
}
