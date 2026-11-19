using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using WorkSite.Core.Hubs;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class ProjectFeedHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectFeedHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private Task SendAsync(string projectId, string projectName)
    {
        var hub = _factory.Services.GetRequiredService<IHubContext<ProjectFeedHub, IProjectFeedClient>>();
        var id = Guid.Parse(projectId);
        return hub.Clients.Group(ProjectFeedGroups.GroupOf(id))
            .Activity(ProjectFeedTicker.Invent(id, projectName, new Random(3), new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task a_connection_hears_only_the_feeds_it_joined_and_stops_when_it_leaves()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var joined = await TestUsers.CreateProjectAsync(client, clientId, "Joined");
        var ignored = await TestUsers.CreateProjectAsync(client, clientId, "Ignored");
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/project-feed", client);
        await connection.InvokeAsync("Join", joined);

        //Act
        await SendAsync(ignored, "Ignored");
        await SendAsync(joined, "Joined");
        var heard = await connection.NextAsync("Activity", TimeSpan.FromSeconds(5));
        await connection.InvokeAsync("Leave", joined);
        await SendAsync(joined, "Joined");

        //Assert
        heard[0].GetProperty("projectId").GetString().Should().Be(joined);
        heard[0].GetProperty("projectName").GetString().Should().Be("Joined");
        heard[0].GetProperty("person").GetString().Should().NotBeNullOrEmpty();
        heard[0].GetProperty("text").GetString().Should().StartWith("logged ");
        (await connection.StaysSilentAsync("Activity", TimeSpan.FromSeconds(1.5))).Should().BeTrue();
    }

    [Fact]
    public async Task Join_refuses_a_project_of_another_user()
    {
        //Arrange
        using var owner = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(owner);
        var projectId = await TestUsers.CreateProjectAsync(owner, await TestUsers.CreateClientAsync(owner));
        using var other = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(other);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/project-feed", other);

        //Act
        Func<Task> act = () => connection.InvokeAsync("Join", projectId);

        //Assert
        (await act.Should().ThrowAsync<HubTestException>()).Which.Message.Should().Be("Choose one of your projects.");
    }

    [Fact]
    public void Listened_names_each_project_that_still_has_a_listener()
    {
        //Arrange
        var groups = new ProjectFeedGroups();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        groups.Join("a", first, "First");
        groups.Join("b", first, "First");
        groups.Join("b", second, "Second");

        //Act
        groups.Leave("a", first);
        var afterLeave = groups.Listened().OrderBy(p => p.ProjectName).ToList();
        groups.Disconnected("b");

        //Assert
        afterLeave.Select(p => p.ProjectName).Should().Equal("First", "Second");
        groups.Listened().Should().BeEmpty();
        ProjectFeedGroups.GroupOf(first).Should().Be("project:" + first.ToString("N"));
    }

    [Fact]
    public void Invent_makes_up_quarter_hours_for_a_named_person()
    {
        //Arrange
        var projectId = Guid.NewGuid();

        //Act
        var activity = ProjectFeedTicker.Invent(projectId, "Site build", new Random(11), new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero));

        //Assert
        activity.ProjectId.Should().Be(projectId);
        activity.ProjectName.Should().Be("Site build");
        activity.Hours.Should().BeInRange(0.25m, 4m);
        (activity.Hours % 0.25m).Should().Be(0m);
        activity.Text.Should().Be(FormattableString.Invariant($"logged {activity.Hours:0.##} h"));
        activity.At.Should().Be(new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc));
    }
}
