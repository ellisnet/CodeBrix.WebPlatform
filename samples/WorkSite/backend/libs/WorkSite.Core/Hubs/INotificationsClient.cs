using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the notifications hub sends.</summary>
[WireRequired]
public interface INotificationsClient
{
    /// <summary>Sent at irregular intervals.</summary>
    /// <param name="notification">The reminder.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Notify(Notification notification);
}
