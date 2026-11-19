using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hosting;

public class EmptyWebRootTests : IClassFixture<EmptyWebRootApiFactory>
{
    private readonly EmptyWebRootApiFactory _factory;

    public EmptyWebRootTests(EmptyWebRootApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task the_api_works_before_the_frontend_is_built()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var health = await client.GetAsync("/api/health");
        await TestUsers.RegisterAsync(client);
        var clients = await client.GetAsync("/api/clients");

        //Assert
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        clients.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task app_routes_are_404_without_an_index_html()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/app/clients");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
