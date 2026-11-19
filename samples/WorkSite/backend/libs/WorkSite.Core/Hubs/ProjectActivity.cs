using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>A (made-up) piece of teammate activity on a project.</summary>
[WireRequired]
public sealed class ProjectActivity
{
    /// <summary>The project the activity is on.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The project's name.</summary>
    public string ProjectName { get; set; }

    /// <summary>The teammate's name.</summary>
    public string Person { get; set; }

    /// <summary>What they did, such as <c>logged 2 h</c>.</summary>
    public string Text { get; set; }

    /// <summary>The hours involved.</summary>
    public decimal Hours { get; set; }

    /// <summary>When it happened, UTC.</summary>
    public DateTime At { get; set; }
}
