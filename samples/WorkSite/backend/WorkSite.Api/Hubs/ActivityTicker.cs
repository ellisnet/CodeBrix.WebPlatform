using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Nudges the activity figures every one and a half to five seconds and sends them to everyone.</summary>
public sealed class ActivityTicker : BackgroundService
{
    private readonly IHubContext<ActivityHub, IActivityClient> _hub;
    private readonly ActivityFeed _feed;
    private readonly TimeProvider _time;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="feed">The figures.</param>
    /// <param name="time">The clock.</param>
    public ActivityTicker(IHubContext<ActivityHub, IActivityClient> hub, ActivityFeed feed, TimeProvider time)
    {
        _hub = hub;
        _feed = feed;
        _time = time;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(1500, 5000)), _time, stoppingToken);
            await _hub.Clients.All.Stats(_feed.Advance());
        }
    }
}
