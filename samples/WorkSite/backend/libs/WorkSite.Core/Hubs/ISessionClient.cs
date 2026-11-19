using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the session hub sends.</summary>
[WireRequired]
public interface ISessionClient
{
    /// <summary>Sent to the connections of a session that has just ended, so their pages sign out at once.</summary>
    /// <param name="ended">Why the session ended.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Ended(SessionEnded ended);
}
