using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Security;

public class SecurityHeadersTests : IClassFixture<ApiFactory>
{
    private const string DefaultPolicy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private readonly ApiFactory _factory;

    public SecurityHeadersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/no-such-route")]
    [InlineData("/")]
    [InlineData("/app/time")]
    [InlineData("/styles.css")]
    [InlineData("/missing.png")]
    public async Task every_response_carries_the_security_headers(string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync(path);

        //Assert
        Header(response, "X-Content-Type-Options").Should().Be("nosniff");
        Header(response, "X-Frame-Options").Should().Be("DENY");
        Header(response, "Referrer-Policy").Should().Be("no-referrer");
        Header(response, "Content-Security-Policy").Should().Be(DefaultPolicy);
    }

    [Fact]
    public async Task error_responses_carry_them_too()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/clients", new { }, withRequestedWith: false);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Header(response, "X-Frame-Options").Should().Be("DENY");
    }

    [Fact]
    public async Task development_sends_no_hsts()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/health");

        //Assert
        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    internal static string Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.Single()
            : response.Content.Headers.TryGetValues(name, out var contentValues) ? contentValues.Single() : null;
}
