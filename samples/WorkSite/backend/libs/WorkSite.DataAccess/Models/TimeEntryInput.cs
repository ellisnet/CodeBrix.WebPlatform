using System;

namespace WorkSite.DataAccess.Models;

/// <summary>The editable values of a time entry.</summary>
public sealed class TimeEntryInput
{
    /// <summary>The project the hours were worked on.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The day the work was done.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>The hours worked.</summary>
    public decimal Hours { get; set; }

    /// <summary>What was done, or "".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the hours are billable.</summary>
    public bool IsBillable { get; set; }
}
