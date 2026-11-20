using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ApplicationSplitter.Api.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace ApplicationSplitter.Api.Hubs;

/// <summary>Sends the server's time to every page connected to the clock hub, once a second.</summary>
public sealed class ClockTicker : BackgroundService
{
    private readonly IHubContext<ClockHub, IClockClient> _hub;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The clock hub.</param>
    public ClockTicker(IHubContext<ClockHub, IClockClient> hub)
    {
        _hub = hub;
    }

    /// <summary>The message for a moment.</summary>
    /// <param name="now">The moment.</param>
    /// <param name="tick">The number of the tick.</param>
    /// <returns>The message.</returns>
    public static ServerClock Read(DateTimeOffset now, long tick)
        => new ServerClock { UtcTime = now.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture), Tick = tick };

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long tick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _hub.Clients.All.Tick(Read(DateTimeOffset.UtcNow, ++tick));
        }
    }
}
