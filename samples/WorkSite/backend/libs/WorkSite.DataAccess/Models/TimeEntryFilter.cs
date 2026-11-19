using System;

namespace WorkSite.DataAccess.Models;

/// <summary>Which time entries to list; every filter is optional.</summary>
public sealed class TimeEntryFilter
{
    /// <summary>The first work date to include.</summary>
    public DateOnly? From { get; set; }

    /// <summary>The last work date to include.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Only entries for this client's projects.</summary>
    public Guid? ClientId { get; set; }

    /// <summary>Only entries for this project.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Only billable (true) or only non-billable (false) entries.</summary>
    public bool? Billable { get; set; }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>The page size.</summary>
    public int PageSize { get; set; } = 25;
}
