using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>A piece of work for a client, with computed totals.</summary>
[WireRequired]
public sealed class Project
{
    /// <summary>The project's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The client the project belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>The client's name.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>The project's name, unique per client ignoring case.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A description, or "".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The hourly rate in US dollars, or null for no rate.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>The default billable flag for new time entries on this project.</summary>
    public bool IsBillable { get; set; }

    /// <summary>Whether the project is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>When the project was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The hours recorded on the project.</summary>
    public decimal TotalHours { get; set; }
}
