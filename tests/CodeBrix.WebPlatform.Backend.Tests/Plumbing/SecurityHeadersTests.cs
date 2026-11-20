using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

public class SecurityHeadersTests : IClassFixture<PlumbingFixture>
{
    private readonly BackendHost _host;

    public SecurityHeadersTests(PlumbingFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData("GET", "/api/echo")]
    [InlineData("GET", "/api/account")]
    [InlineData("GET", "/api/no-such-route")]
    [InlineData("POST", "/api/echo")]
    [InlineData("GET", "/")]
    [InlineData("GET", "/app/time")]
    [InlineData("GET", "/assets/styles.css")]
    [InlineData("GET", "/app.wasm")]
    [InlineData("GET", "/missing.png")]
    [InlineData("POST", "/features")]
    [InlineData("POST", "/api/hubs/echo/negotiate")]
    public async Task every_answer_carries_the_security_headers_and_the_applications_policy(string method, string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path);

        //Assert
        response.Header("X-Content-Type-Options").Should().Be("nosniff");
        response.Header("X-Frame-Options").Should().Be("DENY");
        response.Header("Referrer-Policy").Should().Be("no-referrer");
        response.Header("Content-Security-Policy").Should().Be(PlumbingFixture.Policy);
    }

    [Fact]
    public async Task no_hsts_is_sent_by_the_library()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Header("Strict-Transport-Security").Should().BeNull();
    }

    [Fact]
    public async Task without_a_policy_in_the_options_no_Content_Security_Policy_is_sent()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync();
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo");

        //Assert
        response.Header("Content-Security-Policy").Should().BeNull();
        response.Header("X-Frame-Options").Should().Be("DENY");
    }

    [Fact]
    public async Task a_blank_policy_counts_as_none()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.ContentSecurityPolicy = "   ");
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo");

        //Assert
        response.Header("Content-Security-Policy").Should().BeNull();
    }

    [Fact]
    public async Task switched_off_no_security_header_is_sent_not_even_the_policy()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options =>
        {
            options.SendSecurityHeaders = false;
            options.ContentSecurityPolicy = PlumbingFixture.Policy;
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Header("X-Content-Type-Options").Should().BeNull();
        response.Header("X-Frame-Options").Should().BeNull();
        response.Header("Referrer-Policy").Should().BeNull();
        response.Header("Content-Security-Policy").Should().BeNull();
    }
}
