using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/time-entries</c> and <c>PUT /api/time-entries/{id}</c>.</summary>
[WireRequired]
public sealed class TimeEntryRequest
{
    /// <summary>The project the hours were worked on.</summary>
    [WireRequired]
    public Guid? ProjectId { get; set; }

    /// <summary>The day the work was done, at most one day in the future.</summary>
    [WireRequired]
    public DateOnly? WorkDate { get; set; }

    /// <summary>The hours worked: more than 0, at most 24, two decimals at most.</summary>
    [WireRequired]
    public decimal? Hours { get; set; }

    /// <summary>What was done, at most 500 characters; may be empty.</summary>
    public string Description { get; set; }

    /// <summary>Whether the hours are billable; when absent, the project's default on POST and unchanged on PUT.</summary>
    [WireRequired]
    public bool? IsBillable { get; set; }
}
