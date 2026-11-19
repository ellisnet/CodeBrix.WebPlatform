using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>A customer the user records time for, with computed totals.</summary>
[WireRequired]
public sealed class Client
{
    /// <summary>The client's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The client's name, unique per user ignoring case.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The contact person's name, or "".</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>The contact person's email address, or "".</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Free-form notes, or "".</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>The badge colour family (see <see cref="ClientColors"/>).</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Whether the client is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>When the client was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>How many projects the client has, archived ones included.</summary>
    public int ProjectCount { get; set; }

    /// <summary>The hours recorded on all of the client's projects.</summary>
    public decimal TotalHours { get; set; }
}
