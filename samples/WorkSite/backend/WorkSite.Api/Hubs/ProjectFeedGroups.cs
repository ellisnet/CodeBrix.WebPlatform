using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkSite.Api.Hubs;

/// <summary>Which connections listen to which project's activity feed.</summary>
public sealed class ProjectFeedGroups
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<Guid>> _byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> _names = new();

    /// <summary>The SignalR group of a project's feed.</summary>
    /// <param name="projectId">The project.</param>
    /// <returns>The group name.</returns>
    public static string GroupOf(Guid projectId) => "project:" + projectId.ToString("N");

    /// <summary>Records that a connection joined a project's feed.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="projectId">The project.</param>
    /// <param name="projectName">The project's name.</param>
    public void Join(string connectionId, Guid projectId, string projectName)
    {
        lock (_gate)
        {
            if (!_byConnection.TryGetValue(connectionId, out var projects))
            {
                projects = new HashSet<Guid>();
                _byConnection[connectionId] = projects;
            }

            projects.Add(projectId);
            _names[projectId] = projectName;
        }
    }

    /// <summary>Records that a connection left a project's feed.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="projectId">The project.</param>
    public void Leave(string connectionId, Guid projectId)
    {
        lock (_gate)
        {
            if (_byConnection.TryGetValue(connectionId, out var projects) && projects.Remove(projectId) && projects.Count == 0)
            {
                _byConnection.Remove(connectionId);
            }
        }
    }

    /// <summary>Forgets a connection that has closed.</summary>
    /// <param name="connectionId">The connection.</param>
    public void Disconnected(string connectionId)
    {
        lock (_gate)
        {
            _byConnection.Remove(connectionId);
        }
    }

    /// <summary>The projects that have at least one listener, with their names.</summary>
    /// <returns>The projects.</returns>
    public IReadOnlyList<(Guid ProjectId, string ProjectName)> Listened()
    {
        lock (_gate)
        {
            return _byConnection.Values.SelectMany(p => p).Distinct().Select(id => (id, _names[id])).ToList();
        }
    }
}
