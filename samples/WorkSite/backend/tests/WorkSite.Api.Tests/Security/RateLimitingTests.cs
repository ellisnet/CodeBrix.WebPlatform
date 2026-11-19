using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Security;

public class RateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitingTests(RateLimitedApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/refresh")]
    public async Task requests_over_the_limit_answer_429_with_the_contract_body(string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            var allowed = await client.PostAsync(path, new { });
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        //Act
        var response = await client.PostAsync(path, new { });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
        problem.TryGetProperty("errors", out _).Should().BeFalse();
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task other_routes_are_not_limited()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit * 3; i++)
        {
            await client.GetAsync("/api/health");
        }

        //Act
        var response = await client.GetAsync("/api/health");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
