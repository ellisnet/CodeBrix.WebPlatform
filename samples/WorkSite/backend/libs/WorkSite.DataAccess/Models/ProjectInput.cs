using System;

namespace WorkSite.DataAccess.Models;

/// <summary>The editable values of a project.</summary>
public sealed class ProjectInput
{
    /// <summary>The client the project belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>The project's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A description, or "".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The hourly rate, or null for no rate.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>The default billable flag for new entries.</summary>
    public bool IsBillable { get; set; }

    /// <summary>Whether the project is archived.</summary>
    public bool IsArchived { get; set; }
}
