using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>Hours worked on one project on one day.</summary>
[WireRequired]
public sealed class TimeEntry
{
    /// <summary>The entry's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The project the hours were worked on.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The project's name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>The project's client.</summary>
    public Guid ClientId { get; set; }

    /// <summary>The client's name.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>The day the work was done.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>The hours worked, more than 0 and at most 24, two decimals.</summary>
    public decimal Hours { get; set; }

    /// <summary>What was done, or "".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the hours are billable.</summary>
    public bool IsBillable { get; set; }

    /// <summary>When the entry was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the entry was last changed (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
