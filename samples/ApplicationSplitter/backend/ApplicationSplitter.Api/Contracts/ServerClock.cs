using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>The server's time of day, sent once a second by the clock hub.</summary>
[WireRequired]
public sealed class ServerClock
{
    /// <summary>The time in UTC, as "HH:mm:ss".</summary>
    public string UtcTime { get; set; }

    /// <summary>How many ticks the server has sent since it started.</summary>
    public long Tick { get; set; }
}
