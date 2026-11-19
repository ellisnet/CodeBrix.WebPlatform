using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Api.Auth;
using WorkSite.Api.Validation;
using WorkSite.Core.Hubs;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The running timer: start one against a project, watch it tick in every open tab, and stop it
/// to turn the elapsed time into a time entry. Needs sign-in.
/// </summary>
[WireHub("/api/hubs/timer")]
[Authorize]
[WireRequired]
public sealed class TimerHub : Hub<ITimerClient>
{
    private readonly RunningTimers _timers;
    private readonly IProjectRepository _projects;
    private readonly ITimeEntryRepository _entries;
    private readonly ChangeNotifier _changes;
    private readonly TimeProvider _time;

    /// <summary>Creates the hub.</summary>
    /// <param name="timers">The running timers.</param>
    /// <param name="projects">The project store.</param>
    /// <param name="entries">The time-entry store.</param>
    /// <param name="changes">Tells the user's other tabs about the new entry.</param>
    /// <param name="time">The clock.</param>
    public TimerHub(RunningTimers timers, IProjectRepository projects, ITimeEntryRepository entries, ChangeNotifier changes,
        TimeProvider time)
    {
        _timers = timers;
        _projects = projects;
        _entries = entries;
        _changes = changes;
        _time = time;
    }

    private Guid UserId => Context.User.GetUserId();

    /// <summary>The signed-in user's timer as it is now.</summary>
    /// <returns>The timer, or "not running".</returns>
    public TimerState Get() => _timers.State(UserId, _time.GetUtcNow());

    /// <summary>Starts a timer on one of the user's projects, replacing a timer that is already running.</summary>
    /// <param name="projectId">The project.</param>
    /// <param name="description">What is being worked on; may be empty.</param>
    /// <returns>The timer.</returns>
    /// <exception cref="HubException">The project is not the user's, or is archived.</exception>
    public async Task<TimerState> Start(Guid projectId, string description)
    {
        var userId = UserId;
        var project = await _projects.GetAsync(userId, projectId, Context.ConnectionAborted);
        if (project == null)
        {
            throw new HubException("Choose one of your projects.");
        }

        if (project.IsArchived)
        {
            throw new HubException("This project is archived. Choose another project.");
        }

        var text = (description ?? string.Empty).Trim();
        if (text.Length > InputRules.MaxEntryDescriptionLength)
        {
            throw new HubException($"The description can be at most {InputRules.MaxEntryDescriptionLength} characters.");
        }

        _timers.Start(userId, new RunningTimer
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ClientName = project.ClientName,
            IsBillable = project.IsBillable,
            Description = text,
            StartedAt = _time.GetUtcNow(),
        });
        var state = _timers.State(userId, _time.GetUtcNow());
        await Clients.User(HubUserIdProvider.IdOf(userId)).TimerChanged(state);
        return state;
    }

    /// <summary>
    /// Stops the timer and records the elapsed time as a time entry dated today (UTC). The hours
    /// are rounded to two decimals, with a minimum of 0.01.
    /// </summary>
    /// <returns>The new time entry.</returns>
    /// <exception cref="HubException">No timer is running.</exception>
    public async Task<TimeEntry> Stop()
    {
        var userId = UserId;
        var timer = _timers.Remove(userId);
        if (timer == null)
        {
            throw new HubException("No timer is running.");
        }

        var now = _time.GetUtcNow();
        var hours = Math.Clamp(Math.Round((decimal)(now - timer.StartedAt).TotalHours, 2, MidpointRounding.AwayFromZero), 0.01m, 24m);
        TimeEntry entry;
        try
        {
            entry = await _entries.CreateAsync(userId, new TimeEntryInput
            {
                ProjectId = timer.ProjectId,
                WorkDate = DateOnly.FromDateTime(now.UtcDateTime),
                Hours = hours,
                Description = timer.Description,
                IsBillable = timer.IsBillable,
            }, Context.ConnectionAborted);
        }
        catch (ReferenceNotFoundException)
        {
            throw new HubException("The project no longer exists, so the time could not be recorded.");
        }

        await Clients.User(HubUserIdProvider.IdOf(userId)).TimerChanged(new TimerState());
        await _changes.NotifyAsync(userId, ChangedEntity.TimeEntry, ChangeKind.Created, entry.Id);
        return entry;
    }

    /// <summary>Stops the timer without recording anything. Does nothing when no timer is running.</summary>
    /// <returns>A task that completes when every tab has been told.</returns>
    public async Task Discard()
    {
        var userId = UserId;
        _timers.Remove(userId);
        await Clients.User(HubUserIdProvider.IdOf(userId)).TimerChanged(new TimerState());
    }
}
