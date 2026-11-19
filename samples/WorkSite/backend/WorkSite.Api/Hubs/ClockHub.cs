using System;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The world clocks on the public pages: the local time in Paris, New York and Los Angeles, sent
/// to everyone once a second by <see cref="ClockTicker"/>. Open to visitors who are not signed in.
/// </summary>
[WireHub("/api/hubs/clock")]
[AllowAnonymous]
[WireRequired]
public sealed class ClockHub : Hub<IClockClient>
{
    private readonly TimeProvider _time;

    /// <summary>Creates the hub.</summary>
    /// <param name="time">The clock.</param>
    public ClockHub(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>The clocks right now, so a page can show them before the next tick arrives.</summary>
    /// <returns>The local time in each city.</returns>
    public ClockTick Now() => WorldClock.At(_time.GetUtcNow());
}
