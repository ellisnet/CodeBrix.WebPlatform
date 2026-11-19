using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Every three to eight seconds, sends one made-up piece of activity to each project feed that has listeners.</summary>
public sealed class ProjectFeedTicker : BackgroundService
{
    private static readonly string[] People = ["Maria", "Jonas", "Aiko", "Priya", "Tomás", "Lena"];

    private readonly IHubContext<ProjectFeedHub, IProjectFeedClient> _hub;
    private readonly ProjectFeedGroups _groups;
    private readonly TimeProvider _time;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="groups">Who listens to what.</param>
    /// <param name="time">The clock.</param>
    public ProjectFeedTicker(IHubContext<ProjectFeedHub, IProjectFeedClient> hub, ProjectFeedGroups groups, TimeProvider time)
    {
        _hub = hub;
        _groups = groups;
        _time = time;
    }

    /// <summary>A made-up piece of activity on a project.</summary>
    /// <param name="projectId">The project.</param>
    /// <param name="projectName">The project's name.</param>
    /// <param name="random">The source of the made-up values.</param>
    /// <param name="at">When it happened.</param>
    /// <returns>The activity.</returns>
    public static ProjectActivity Invent(Guid projectId, string projectName, Random random, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(random);
        var hours = random.Next(1, 17) * 0.25m;
        return new ProjectActivity
        {
            ProjectId = projectId,
            ProjectName = projectName,
            Person = People[random.Next(People.Length)],
            Text = FormattableString.Invariant($"logged {hours:0.##} h"),
            Hours = hours,
            At = at.UtcDateTime,
        };
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(3000, 8000)), _time, stoppingToken);
            foreach (var (projectId, projectName) in _groups.Listened())
            {
                await _hub.Clients.Group(ProjectFeedGroups.GroupOf(projectId))
                    .Activity(Invent(projectId, projectName, Random.Shared, _time.GetUtcNow()));
            }
        }
    }
}
