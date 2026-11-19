using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class DashboardEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DashboardEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task get_returns_the_contract_shape_for_the_given_day()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client, "Amber Co", "amber");
        var projectId = await TestUsers.CreateProjectAsync(client, clientId, "Build", hourlyRate: 100m);
        await TestUsers.CreateEntryAsync(client, projectId, "2026-11-16", 3m);                    //Monday this week
        await TestUsers.CreateEntryAsync(client, projectId, "2026-11-18", 2.5m);                  //today
        await TestUsers.CreateEntryAsync(client, projectId, "2026-11-12", 4m, isBillable: false); //last week

        //Act
        var response = await client.GetAsync("/api/dashboard?today=2026-11-18");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("hoursToday", "hoursThisWeek", "hoursLastWeek",
            "hoursThisMonth", "billableHoursThisMonth", "amountThisMonth", "activeProjectCount", "activeClientCount",
            "dailyHours", "topProjects", "recentEntries");
        body.GetProperty("hoursToday").GetDecimal().Should().Be(2.5m);
        body.GetProperty("hoursThisWeek").GetDecimal().Should().Be(5.5m);
        body.GetProperty("hoursLastWeek").GetDecimal().Should().Be(4m);
        body.GetProperty("hoursThisMonth").GetDecimal().Should().Be(9.5m);
        body.GetProperty("billableHoursThisMonth").GetDecimal().Should().Be(5.5m);
        body.GetProperty("amountThisMonth").GetDecimal().Should().Be(550m);
        body.GetProperty("activeProjectCount").GetInt32().Should().Be(1);
        body.GetProperty("activeClientCount").GetInt32().Should().Be(1);
        var daily = body.GetProperty("dailyHours").EnumerateArray().ToList();
        daily.Should().HaveCount(14);
        daily[0].GetProperty("date").GetString().Should().Be("2026-11-05");
        daily[13].GetProperty("date").GetString().Should().Be("2026-11-18");
        daily[13].GetProperty("billableHours").GetDecimal().Should().Be(2.5m);
        var top = body.GetProperty("topProjects")[0];
        top.GetProperty("projectId").GetString().Should().Be(projectId);
        top.GetProperty("projectName").GetString().Should().Be("Build");
        top.GetProperty("clientName").GetString().Should().Be("Amber Co");
        top.GetProperty("color").GetString().Should().Be("amber");
        top.GetProperty("hours").GetDecimal().Should().Be(9.5m);
        body.GetProperty("recentEntries").EnumerateArray().Select(e => e.GetProperty("workDate").GetString())
            .Should().Equal("2026-11-18", "2026-11-16", "2026-11-12");
    }

    [Fact]
    public async Task get_without_today_uses_the_servers_utc_date()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var body = await ApiClient.JsonAsync(await client.GetAsync("/api/dashboard"));

        //Assert
        body.GetProperty("dailyHours")[13].GetProperty("date").GetString()
            .Should().Be(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task get_rejects_a_malformed_today()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.GetAsync("/api/dashboard?today=tomorrow");

        //Assert
        (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("today");
    }
}
