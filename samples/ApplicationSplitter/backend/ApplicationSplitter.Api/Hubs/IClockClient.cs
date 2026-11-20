using System.Threading.Tasks;
using ApplicationSplitter.Api.Contracts;
using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Hubs;

/// <summary>What the clock hub sends to every connected page.</summary>
[WireRequired]
public interface IClockClient
{
    /// <summary>The server's time, once a second.</summary>
    /// <param name="clock">The time.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Tick(ServerClock clock);
}
