using System;
using ApplicationSplitter.Api.Contracts;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApplicationSplitter.Api.Hubs;

/// <summary>
/// A public hub that tells the server's time. Every frontend application connects to it, which
/// shows that the platform's SignalR client works from an application served below a name.
/// </summary>
[WireHub("/api/hubs/clock")]
[AllowAnonymous]
[WireRequired]
public sealed class ClockHub : Hub<IClockClient>
{
    /// <summary>The server's time now.</summary>
    /// <returns>The time.</returns>
    public ServerClock Now() => ClockTicker.Read(DateTimeOffset.UtcNow, 0);
}
