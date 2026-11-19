using System.Net.Http;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Security;

public class ProductionHeadersTests : IClassFixture<ProductionApiFactory>
{
    private readonly ProductionApiFactory _factory;

    public ProductionHeadersTests(ProductionApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task production_sends_hsts_on_https()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        //HSTS is never sent for localhost, so ask for a real-looking host name.
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://worksite.example/api/health"));

        //Assert
        SecurityHeadersTests.Header(response, "Strict-Transport-Security").Should().StartWith("max-age=");
    }

    [Fact]
    public async Task the_content_security_policy_comes_from_configuration()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/health");

        //Assert
        SecurityHeadersTests.Header(response, "Content-Security-Policy").Should().Be(ProductionApiFactory.ContentSecurityPolicy);
    }
}
