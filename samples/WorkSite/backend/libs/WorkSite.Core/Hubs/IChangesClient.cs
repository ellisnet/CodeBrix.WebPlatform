using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the data-changed hub sends to every connection of the user whose data changed.</summary>
[WireRequired]
public interface IChangesClient
{
    /// <summary>Sent after a client, project or time entry is created, changed or deleted.</summary>
    /// <param name="change">What changed.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Changed(DataChange change);
}
