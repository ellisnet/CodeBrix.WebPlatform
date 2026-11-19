using System;

namespace WorkSite.Core.Models;

/// <summary>The hours of one day.</summary>
public sealed class DailyHours
{
    /// <summary>The day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>All hours that day.</summary>
    public decimal Hours { get; set; }

    /// <summary>Billable hours that day.</summary>
    public decimal BillableHours { get; set; }
}
