using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Sends the world clocks to every connection of <see cref="ClockHub"/> once a second.</summary>
public sealed class ClockTicker : BackgroundService
{
    private readonly IHubContext<ClockHub, IClockClient> _hub;
    private readonly TimeProvider _time;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="time">The clock.</param>
    public ClockTicker(IHubContext<ClockHub, IClockClient> hub, TimeProvider time)
    {
        _hub = hub;
        _time = time;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _hub.Clients.All.Tick(WorldClock.At(_time.GetUtcNow()));
        }
    }
}
