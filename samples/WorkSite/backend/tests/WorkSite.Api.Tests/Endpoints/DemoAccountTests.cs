using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class DemoAccountTests : IClassFixture<DemoApiFactory>
{
    private readonly DemoApiFactory _factory;

    public DemoAccountTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task meta_offers_the_demo_account_without_its_password()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/meta");

        //Assert
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.Should().NotContain(DemoApiFactory.Password);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("demoAccount").GetProperty("enabled").GetBoolean().Should().BeTrue();
        body.GetProperty("demoAccount").GetProperty("username").GetString().Should().Be(DemoApiFactory.Username);
    }

    [Fact]
    public async Task the_demo_account_signs_in_and_has_history()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var login = await client.PostAsync("/api/auth/login",
            new { identifier = DemoApiFactory.Username, password = DemoApiFactory.Password });
        var clients = await ApiClient.JsonAsync(await client.GetAsync("/api/clients"));
        var entries = await ApiClient.JsonAsync(await client.GetAsync("/api/time-entries?pageSize=1"));
        var dashboard = await ApiClient.JsonAsync(await client.GetAsync("/api/dashboard"));

        //Assert
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        clients.GetArrayLength().Should().BeGreaterThan(3);
        entries.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(200);
        dashboard.GetProperty("recentEntries").GetArrayLength().Should().Be(8);
    }
}
