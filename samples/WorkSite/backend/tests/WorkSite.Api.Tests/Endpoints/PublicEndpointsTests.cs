using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class PublicEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PublicEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task health_reports_ok_and_the_schema_version()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/health");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("ok");
        body.GetProperty("databaseVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task meta_names_the_application_and_reports_the_demo_account_disabled()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/meta");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("name").GetString().Should().Be("WorkSite");
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("demoAccount").GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("demoAccount").GetProperty("username").GetString().Should().Be("");
    }
}
