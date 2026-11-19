using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The timers that are running, one at most per user. They are kept in memory: a restart of the
/// backend forgets them, which is acceptable for a demonstration.
/// </summary>
public sealed class RunningTimers
{
    private readonly ConcurrentDictionary<Guid, RunningTimer> _timers = new();

    /// <summary>The users who have a timer running.</summary>
    public IReadOnlyCollection<Guid> Users => _timers.Keys.ToList();

    /// <summary>Starts (or restarts) a user's timer.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="timer">The timer.</param>
    public void Start(Guid userId, RunningTimer timer) => _timers[userId] = timer;

    /// <summary>A user's running timer, or null.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The timer, or null when none is running.</returns>
    public RunningTimer Get(Guid userId) => _timers.TryGetValue(userId, out var timer) ? timer : null;

    /// <summary>Removes a user's timer.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The timer that was running, or null.</returns>
    public RunningTimer Remove(Guid userId) => _timers.TryRemove(userId, out var timer) ? timer : null;

    /// <summary>A user's timer as it is sent to the frontend.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The state; "not running" when the user has no timer.</returns>
    public TimerState State(Guid userId, DateTimeOffset now)
    {
        var timer = Get(userId);
        if (timer == null)
        {
            return new TimerState();
        }

        return new TimerState
        {
            IsRunning = true,
            ProjectId = timer.ProjectId,
            ProjectName = timer.ProjectName,
            ClientName = timer.ClientName,
            Description = timer.Description,
            StartedAt = timer.StartedAt.UtcDateTime,
            ElapsedSeconds = Math.Max(0, (int)(now - timer.StartedAt).TotalSeconds),
        };
    }
}
