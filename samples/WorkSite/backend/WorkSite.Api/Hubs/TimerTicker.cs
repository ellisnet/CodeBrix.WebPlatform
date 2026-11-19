using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Sends each running timer to every connection of its user once a second.</summary>
public sealed class TimerTicker : BackgroundService
{
    private readonly IHubContext<TimerHub, ITimerClient> _hub;
    private readonly RunningTimers _timers;
    private readonly TimeProvider _time;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="timers">The running timers.</param>
    /// <param name="time">The clock.</param>
    public TimerTicker(IHubContext<TimerHub, ITimerClient> hub, RunningTimers timers, TimeProvider time)
    {
        _hub = hub;
        _timers = timers;
        _time = time;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = _time.GetUtcNow();
            foreach (var userId in _timers.Users)
            {
                var state = _timers.State(userId, now);
                if (state.IsRunning)
                {
                    await _hub.Clients.User(HubUserIdProvider.IdOf(userId)).Tick(state);
                }
            }
        }
    }
}
