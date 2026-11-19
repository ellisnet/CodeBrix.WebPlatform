using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class TimerHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimerHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> NewProjectAsync(ApiClient client, string name = "Timed work")
        => await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client, "Timer Co " + Guid.NewGuid().ToString("N")[..6]), name);

    [Fact]
    public async Task Get_says_not_running_before_a_timer_is_started()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);

        //Act
        var state = await connection.InvokeAsync("Get");

        //Assert
        state.GetProperty("isRunning").GetBoolean().Should().BeFalse();
        state.GetProperty("projectId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        state.GetProperty("projectName").GetString().Should().BeEmpty();
        state.GetProperty("elapsedSeconds").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Start_runs_a_timer_on_the_project_and_tells_every_tab()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await NewProjectAsync(client, "Site build");
        await using var firstTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);
        await using var secondTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);

        //Act
        var state = await firstTab.InvokeAsync("Start", projectId, "  Laying foundations  ");
        var seenElsewhere = await secondTab.NextAsync("TimerChanged", TimeSpan.FromSeconds(5));
        var tick = await secondTab.NextAsync("Tick", TimeSpan.FromSeconds(5));

        //Assert
        state.GetProperty("isRunning").GetBoolean().Should().BeTrue();
        state.GetProperty("projectId").GetString().Should().Be(projectId);
        state.GetProperty("projectName").GetString().Should().Be("Site build");
        state.GetProperty("clientName").GetString().Should().StartWith("Timer Co");
        state.GetProperty("description").GetString().Should().Be("Laying foundations");
        seenElsewhere[0].GetProperty("isRunning").GetBoolean().Should().BeTrue();
        tick[0].GetProperty("projectId").GetString().Should().Be(projectId);
        tick[0].GetProperty("elapsedSeconds").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Start_refuses_a_project_of_another_user()
    {
        //Arrange
        using var owner = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(owner);
        var projectId = await NewProjectAsync(owner);
        using var other = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(other);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", other);

        //Act
        Func<Task> act = () => connection.InvokeAsync("Start", projectId, "");

        //Assert
        (await act.Should().ThrowAsync<HubTestException>()).Which.Message.Should().Be("Choose one of your projects.");
    }

    [Fact]
    public async Task a_timer_of_one_user_is_not_shown_to_another()
    {
        //Arrange
        using var owner = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(owner);
        var projectId = await NewProjectAsync(owner);
        using var other = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(other);
        await using var ownerTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", owner);
        await using var otherTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", other);

        //Act
        await ownerTab.InvokeAsync("Start", projectId, "");

        //Assert
        (await otherTab.StaysSilentAsync("Tick", TimeSpan.FromSeconds(2.5))).Should().BeTrue();
        (await otherTab.InvokeAsync("Get")).GetProperty("isRunning").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Stop_records_the_time_as_an_entry_and_clears_the_timer()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await NewProjectAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);
        await using var changes = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/changes", client);
        await connection.InvokeAsync("Start", projectId, "Short task");

        //Act
        var entry = await connection.InvokeAsync("Stop");
        var changed = await changes.NextAsync("Changed", TimeSpan.FromSeconds(5));

        //Assert
        entry.GetProperty("projectId").GetString().Should().Be(projectId);
        entry.GetProperty("description").GetString().Should().Be("Short task");
        entry.GetProperty("hours").GetDecimal().Should().Be(0.01m);
        (await connection.InvokeAsync("Get")).GetProperty("isRunning").GetBoolean().Should().BeFalse();
        changed[0].GetProperty("entity").GetString().Should().Be("TimeEntry");
        changed[0].GetProperty("kind").GetString().Should().Be("Created");
        changed[0].GetProperty("id").GetString().Should().Be(entry.GetProperty("id").GetString());
        var listed = await ApiClient.JsonAsync(await client.GetAsync("/api/time-entries"));
        listed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).Should().Contain(entry.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Stop_says_so_when_no_timer_is_running()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);

        //Act
        Func<Task> act = () => connection.InvokeAsync("Stop");

        //Assert
        (await act.Should().ThrowAsync<HubTestException>()).Which.Message.Should().Be("No timer is running.");
    }

    [Fact]
    public async Task Discard_clears_the_timer_without_recording_anything()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await NewProjectAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer", client);
        await connection.InvokeAsync("Start", projectId, "");

        //Act
        await connection.InvokeAsync("Discard");

        //Assert
        (await connection.InvokeAsync("Get")).GetProperty("isRunning").GetBoolean().Should().BeFalse();
        var listed = await ApiClient.JsonAsync(await client.GetAsync("/api/time-entries"));
        listed.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public void State_counts_whole_seconds_since_the_start()
    {
        //Arrange
        var timers = new RunningTimers();
        var userId = Guid.NewGuid();
        var started = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
        timers.Start(userId, new RunningTimer { ProjectId = Guid.NewGuid(), ProjectName = "P", ClientName = "C", StartedAt = started });

        //Act
        var state = timers.State(userId, started.AddSeconds(125.9));

        //Assert
        state.IsRunning.Should().BeTrue();
        state.ElapsedSeconds.Should().Be(125);
        state.StartedAt.Should().Be(started.UtcDateTime);
        timers.Users.Should().Equal(userId);
    }

    [Fact]
    public void Remove_returns_the_timer_once()
    {
        //Arrange
        var timers = new RunningTimers();
        var userId = Guid.NewGuid();
        timers.Start(userId, new RunningTimer { ProjectName = "P" });

        //Act
        var first = timers.Remove(userId);
        var second = timers.Remove(userId);

        //Assert
        first.ProjectName.Should().Be("P");
        second.Should().BeNull();
        timers.State(userId, DateTimeOffset.UtcNow).IsRunning.Should().BeFalse();
    }
}
