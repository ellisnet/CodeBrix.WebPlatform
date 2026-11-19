using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>A project's hours over a period.</summary>
[WireRequired]
public sealed class TopProject
{
    /// <summary>The project's id.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The project's name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>The client's name.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>The client's badge colour.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>The hours in the period.</summary>
    public decimal Hours { get; set; }
}
