using System;

namespace WorkSite.Api.Hubs;

/// <summary>One running timer.</summary>
public sealed class RunningTimer
{
    /// <summary>The project being timed.</summary>
    public Guid ProjectId { get; init; }

    /// <summary>The project's name.</summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>The project's client.</summary>
    public string ClientName { get; init; } = string.Empty;

    /// <summary>Whether the project's hours are billable by default.</summary>
    public bool IsBillable { get; init; }

    /// <summary>What is being worked on.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>When the timer was started.</summary>
    public DateTimeOffset StartedAt { get; init; }
}
