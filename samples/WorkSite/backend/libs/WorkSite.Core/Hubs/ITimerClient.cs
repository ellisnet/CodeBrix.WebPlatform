using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the timer hub sends to every connection of the user who owns the timer.</summary>
[WireRequired]
public interface ITimerClient
{
    /// <summary>Sent once a second while a timer runs.</summary>
    /// <param name="state">The timer with its elapsed time.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Tick(TimerState state);

    /// <summary>Sent when a timer is started, stopped or discarded, so every open tab agrees.</summary>
    /// <param name="state">The timer as it is now.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task TimerChanged(TimerState state);
}
