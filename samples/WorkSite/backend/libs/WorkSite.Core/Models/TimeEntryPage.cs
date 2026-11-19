using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>One page of time entries, with totals over the whole filtered set.</summary>
[WireRequired]
public sealed class TimeEntryPage
{
    /// <summary>The entries on this page, newest work date first.</summary>
    public IReadOnlyList<TimeEntry> Items { get; set; } = [];

    /// <summary>The 1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>The page size.</summary>
    public int PageSize { get; set; }

    /// <summary>How many entries match the filter in total.</summary>
    public int TotalCount { get; set; }

    /// <summary>The hours of every matching entry, not just this page.</summary>
    public decimal TotalHours { get; set; }
}
