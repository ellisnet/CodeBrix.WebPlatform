using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Api.Auth;
using WorkSite.Core.Hubs;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Teammate activity on a project: the frontend joins the feeds of the projects it shows and
/// leaves them again. The activity is made up and sent by <see cref="ProjectFeedTicker"/>. Needs
/// sign-in, and a user can only join the feeds of their own projects.
/// </summary>
[WireHub("/api/hubs/project-feed")]
[Authorize]
[WireRequired]
public sealed class ProjectFeedHub : Hub<IProjectFeedClient>
{
    private readonly ProjectFeedGroups _groups;
    private readonly IProjectRepository _projects;

    /// <summary>Creates the hub.</summary>
    /// <param name="groups">Who listens to what.</param>
    /// <param name="projects">The project store.</param>
    public ProjectFeedHub(ProjectFeedGroups groups, IProjectRepository projects)
    {
        _groups = groups;
        _projects = projects;
    }

    /// <summary>Starts sending this connection a project's activity.</summary>
    /// <param name="projectId">One of the user's projects.</param>
    /// <returns>A task that completes when the connection has joined.</returns>
    /// <exception cref="HubException">The project is not the user's.</exception>
    public async Task Join(Guid projectId)
    {
        var project = await _projects.GetAsync(Context.User.GetUserId(), projectId, Context.ConnectionAborted);
        if (project == null)
        {
            throw new HubException("Choose one of your projects.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectFeedGroups.GroupOf(projectId), Context.ConnectionAborted);
        _groups.Join(Context.ConnectionId, projectId, project.Name);
    }

    /// <summary>Stops sending this connection a project's activity. Does nothing if it had not joined.</summary>
    /// <param name="projectId">The project.</param>
    /// <returns>A task that completes when the connection has left.</returns>
    public async Task Leave(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectFeedGroups.GroupOf(projectId), Context.ConnectionAborted);
        _groups.Leave(Context.ConnectionId, projectId);
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception exception)
    {
        _groups.Disconnected(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
