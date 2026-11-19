using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>One row of a summary report.</summary>
[WireRequired]
public sealed class ReportRow
{
    /// <summary>The group key: an id, a date (yyyy-MM-dd) or a month (yyyy-MM).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The text to show for the row.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Secondary text (the client name on project rows), or "".</summary>
    public string Sublabel { get; set; } = string.Empty;

    /// <summary>The client's badge colour, or "" for period rows.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>All hours in the group.</summary>
    public decimal Hours { get; set; }

    /// <summary>Billable hours in the group.</summary>
    public decimal BillableHours { get; set; }

    /// <summary>The amount of the group's billable hours.</summary>
    public decimal Amount { get; set; }

    /// <summary>The number of entries in the group.</summary>
    public int EntryCount { get; set; }
}
