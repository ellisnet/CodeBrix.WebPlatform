using System;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class EstimatorHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EstimatorHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Estimate_multiplies_hours_by_rate()
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/estimator");

        //Act
        var estimate = await connection.InvokeAsync("Estimate", 7.5m, 120m);

        //Assert
        estimate.GetProperty("hours").GetDecimal().Should().Be(7.5m);
        estimate.GetProperty("rate").GetDecimal().Should().Be(120m);
        estimate.GetProperty("amount").GetDecimal().Should().Be(900m);
        estimate.GetProperty("weeklyAmount").GetDecimal().Should().Be(4500m);
    }

    [Theory]
    [InlineData(0, 100, "Hours must be more than 0 and at most 24.")]
    [InlineData(24.5, 100, "Hours must be more than 0 and at most 24.")]
    [InlineData(8, -1, "The rate must be between 0 and 10,000.")]
    [InlineData(8, 10001, "The rate must be between 0 and 10,000.")]
    public async Task Estimate_answers_out_of_range_input_with_a_sentence_fit_to_show(double hours, double rate, string expected)
    {
        //Arrange
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/estimator");

        //Act
        Func<Task> act = () => connection.InvokeAsync("Estimate", hours, rate);

        //Assert
        (await act.Should().ThrowAsync<HubTestException>()).Which.Message.Should().Be(expected);
    }
}
