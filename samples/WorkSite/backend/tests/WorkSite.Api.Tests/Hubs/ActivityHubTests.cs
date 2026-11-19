using System;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class ActivityHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ActivityHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Current_answers_with_the_three_figures()
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/activity");

        //Act
        var stats = await connection.InvokeAsync("Current");

        //Assert
        stats.GetProperty("hoursLoggedToday").GetDecimal().Should().BeGreaterThan(0m);
        stats.GetProperty("peopleTrackingNow").GetInt32().Should().BeGreaterThan(0);
        stats.GetProperty("entriesToday").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task the_figures_are_pushed_as_they_move()
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/activity");
        var before = (await connection.InvokeAsync("Current")).GetProperty("hoursLoggedToday").GetDecimal();

        //Act
        var stats = await connection.NextAsync("Stats", TimeSpan.FromSeconds(12));

        //Assert
        stats[0].GetProperty("hoursLoggedToday").GetDecimal().Should().BeGreaterThan(before);
    }

    [Fact]
    public void Advance_never_takes_hours_or_entries_back()
    {
        //Arrange
        var feed = new ActivityFeed(new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero), new Random(7));
        var previous = feed.Current;

        //Act + Assert
        for (var i = 0; i < 200; i++)
        {
            var next = feed.Advance();
            next.HoursLoggedToday.Should().BeGreaterThan(previous.HoursLoggedToday);
            next.EntriesToday.Should().BeGreaterThan(previous.EntriesToday);
            next.PeopleTrackingNow.Should().BeInRange(12, 180);
            previous = next;
        }
    }

    [Fact]
    public void Current_does_not_move_the_figures()
    {
        //Arrange
        var feed = new ActivityFeed(new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero), new Random(7));

        //Assert
        feed.Current.HoursLoggedToday.Should().Be(feed.Current.HoursLoggedToday);
        feed.Current.EntriesToday.Should().Be(60 + (570 * 2 / 3));
    }
}
