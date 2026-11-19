using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/projects</c> and <c>PUT /api/projects/{id}</c>.</summary>
[WireRequired]
public sealed class ProjectRequest
{
    /// <summary>The client the project belongs to.</summary>
    [WireRequired]
    public Guid? ClientId { get; set; }

    /// <summary>The project's name, 1 to 120 characters.</summary>
    public string Name { get; set; }

    /// <summary>A description; may be empty.</summary>
    public string Description { get; set; }

    /// <summary>The hourly rate, 0 to 10000, or null for no rate.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>The default billable flag for new entries; true when absent.</summary>
    [WireRequired]
    public bool? IsBillable { get; set; }

    /// <summary>PUT only: whether the project is archived; left unchanged when absent.</summary>
    public bool? IsArchived { get; set; }
}
