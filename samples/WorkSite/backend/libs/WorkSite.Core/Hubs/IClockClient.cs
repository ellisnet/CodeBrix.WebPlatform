using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the world-clock hub sends.</summary>
[WireRequired]
public interface IClockClient
{
    /// <summary>Sent to everyone once a second.</summary>
    /// <param name="tick">The local time in each city.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Tick(ClockTick tick);
}
