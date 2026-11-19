using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the activity hub sends.</summary>
[WireRequired]
public interface IActivityClient
{
    /// <summary>Sent to everyone whenever the figures move, at irregular intervals.</summary>
    /// <param name="stats">The current figures.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Stats(ActivityStats stats);
}
