using System;
using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>One tick of the world clocks: the instant and the local time in each city.</summary>
[WireRequired]
public sealed class ClockTick
{
    /// <summary>The instant of the tick, UTC.</summary>
    public DateTime UtcNow { get; set; }

    /// <summary>The cities, in the order they are shown.</summary>
    public IReadOnlyList<CityTime> Cities { get; set; } = [];
}
