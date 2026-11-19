using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The live activity ticker on the home page: made-up figures sent to everyone at irregular
/// intervals by <see cref="ActivityTicker"/>. Open to visitors who are not signed in.
/// </summary>
[WireHub("/api/hubs/activity")]
[AllowAnonymous]
[WireRequired]
public sealed class ActivityHub : Hub<IActivityClient>
{
    private readonly ActivityFeed _feed;

    /// <summary>Creates the hub.</summary>
    /// <param name="feed">The figures.</param>
    public ActivityHub(ActivityFeed feed)
    {
        _feed = feed;
    }

    /// <summary>The figures right now, so a page can show them before the next change arrives.</summary>
    /// <returns>The current figures.</returns>
    public ActivityStats Current() => _feed.Current;
}
