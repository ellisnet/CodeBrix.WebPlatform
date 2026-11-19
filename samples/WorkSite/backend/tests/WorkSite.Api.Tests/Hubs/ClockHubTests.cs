using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class ClockHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClockHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Now_answers_with_the_three_cities()
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/clock");

        //Act
        var tick = await connection.InvokeAsync("Now");

        //Assert
        var cities = tick.GetProperty("cities").EnumerateArray().ToList();
        cities.Select(c => c.GetProperty("city").GetString()).Should().Equal("Paris", "New York", "Los Angeles");
        foreach (var city in cities)
        {
            Regex.IsMatch(city.GetProperty("time").GetString(), "^[0-2][0-9]:[0-5][0-9]:[0-5][0-9]$").Should().BeTrue();
            Regex.IsMatch(city.GetProperty("date").GetString(), "^[0-9]{4}-[0-9]{2}-[0-9]{2}$").Should().BeTrue();
        }

        tick.GetProperty("utcNow").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task a_tick_arrives_without_being_asked_for()
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/clock");

        //Act
        var first = await connection.NextAsync("Tick", TimeSpan.FromSeconds(5));
        var second = await connection.NextAsync("Tick", TimeSpan.FromSeconds(5));

        //Assert
        first.Length.Should().Be(1);
        first[0].GetProperty("cities").GetArrayLength().Should().Be(3);
        second[0].GetProperty("utcNow").GetString().Should().NotBe(first[0].GetProperty("utcNow").GetString());
    }

    [Fact]
    public void At_works_out_summer_time_in_each_city()
    {
        //Act
        var tick = WorldClock.At(new DateTimeOffset(2026, 7, 1, 12, 0, 5, TimeSpan.Zero));

        //Assert
        tick.Cities.Select(c => c.Time).Should().Equal("14:00:05", "08:00:05", "05:00:05");
        tick.Cities.Select(c => c.UtcOffsetMinutes).Should().Equal(120, -240, -420);
        tick.Cities.Select(c => c.IsDaylightSaving).Should().Equal(true, true, true);
        tick.Cities.Select(c => c.Date).Distinct().Single().Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void At_works_out_winter_time_and_a_different_calendar_day()
    {
        //Act
        var tick = WorldClock.At(new DateTimeOffset(2026, 1, 15, 2, 30, 0, TimeSpan.Zero));

        //Assert
        tick.Cities.Select(c => c.Time).Should().Equal("03:30:00", "21:30:00", "18:30:00");
        tick.Cities.Select(c => c.IsDaylightSaving).Should().Equal(false, false, false);
        tick.Cities.Select(c => c.Date).Should().Equal(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 14));
        tick.Cities.Select(c => c.TimeZone).Should().Equal("Europe/Paris", "America/New_York", "America/Los_Angeles");
        tick.UtcNow.Should().Be(new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Utc));
    }
}
