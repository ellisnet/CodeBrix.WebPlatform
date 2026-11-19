using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>The signed-in user's running timer, or the lack of one.</summary>
[WireRequired]
public sealed class TimerState
{
    /// <summary>True while a timer is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>The project being timed; null when no timer is running.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The project's name; empty when no timer is running.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>The project's client; empty when no timer is running.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>What is being worked on; may be empty.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>When the timer was started, UTC; null when no timer is running.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Whole seconds since the timer was started.</summary>
    public int ElapsedSeconds { get; set; }
}
