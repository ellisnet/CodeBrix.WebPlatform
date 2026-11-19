using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class ReportProgressHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportProgressHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Generate_streams_ten_steps_and_ends_complete()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/report-progress", client);

        //Act
        var stream = await connection.StartStreamAsync("Generate", "2026-11-01", "2026-11-30");
        await connection.CompletionOfAsync(stream);

        //Assert
        var items = connection.ItemsOf(stream);
        items.Select(i => i.GetProperty("percent").GetInt32()).Should().Equal(10, 20, 30, 40, 50, 60, 70, 80, 90, 100);
        items.Select(i => i.GetProperty("isComplete").GetBoolean()).Count(c => c).Should().Be(1);
        items[^1].GetProperty("isComplete").GetBoolean().Should().BeTrue();
        items[^1].GetProperty("stage").GetString().Should().Be("Done");
        items[0].GetProperty("stage").GetString().Should().Be("Collecting time entries");
    }

    [Fact]
    public async Task Generate_refuses_a_range_that_ends_before_it_starts()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/report-progress", client);

        //Act
        var stream = await connection.StartStreamAsync("Generate", "2026-11-30", "2026-11-01");
        Func<Task> act = () => connection.CompletionOfAsync(stream);

        //Assert
        (await act.Should().ThrowAsync<HubTestException>()).Which.Message.Should().Be("The end date must be on or after the start date.");
        connection.ItemsOf(stream).Should().BeEmpty();
    }

    [Fact]
    public async Task cancelling_the_stream_stops_the_work()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/report-progress", client);
        var stream = await connection.StartStreamAsync("Generate", "2026-11-01", "2026-11-30");
        await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken);

        //Act
        await connection.CancelStreamAsync(stream);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        var afterCancel = connection.ItemsOf(stream).Count;
        await Task.Delay(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);

        //Assert
        afterCancel.Should().BeInRange(1, 4);
        connection.ItemsOf(stream).Count.Should().Be(afterCancel);
    }
}
