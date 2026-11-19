using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>What the project-feed hub sends to the connections that joined a project's feed.</summary>
[WireRequired]
public interface IProjectFeedClient
{
    /// <summary>Sent at irregular intervals for each project that has listeners.</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    Task Activity(ProjectActivity activity);
}
