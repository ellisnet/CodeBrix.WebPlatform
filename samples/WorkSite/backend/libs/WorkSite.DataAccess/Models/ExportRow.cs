using System;

namespace WorkSite.DataAccess.Models;

/// <summary>One time entry as it appears in the CSV export.</summary>
public sealed class ExportRow
{
    /// <summary>The day the work was done.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>The client's name.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>The project's name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>The hours worked.</summary>
    public decimal Hours { get; set; }

    /// <summary>Whether the hours are billable.</summary>
    public bool IsBillable { get; set; }

    /// <summary>The project's hourly rate, or null.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>Hours times rate for billable hours with a rate; otherwise null.</summary>
    public decimal? Amount { get; set; }

    /// <summary>What was done.</summary>
    public string Description { get; set; } = string.Empty;
}
