using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.SignIn;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

public class RequestMarkerTests : IClassFixture<PlumbingFixture>
{
    private const string ValidBody = "{\"text\":\"hello\",\"count\":2}";

    private readonly BackendHost _host;

    public RequestMarkerTests(PlumbingFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData("POST", "/api/echo")]
    [InlineData("PUT", "/api/echo/conflict")]
    [InlineData("PATCH", "/api/echo")]
    [InlineData("DELETE", "/api/echo/own-problem")]
    [InlineData("POST", "/api/account/touch")]
    [InlineData("POST", "/api/no-such-route")]
    [InlineData("POST", "/API/Echo")]
    public async Task state_changing_api_requests_without_the_marker_answer_400(string method, string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path, json: ValidBody);

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
        problem.TryGetProperty("errors", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("XMLHttpRequest")]
    [InlineData("samplebackend")]
    [InlineData("SampleBackend ")]
    [InlineData("")]
    public async Task a_wrong_marker_is_refused_too(string value)
    {
        //Arrange
        using var client = _host.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/echo")
        {
            Content = new StringContent(ValidBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(WireRequestMarkerAttribute.HeaderName, value);

        //Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Fact]
    public async Task the_marker_twice_is_refused()
    {
        //Arrange
        using var client = _host.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/echo")
        {
            Content = new StringContent(ValidBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WireRequestMarkerAttribute.HeaderName, new[] { SampleApplication.RequestMarker, SampleApplication.RequestMarker });

        //Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Fact]
    public async Task with_the_backends_declared_marker_the_request_is_answered()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: ValidBody);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be("{\"text\":\"hello\",\"count\":2}");
    }

    [Fact]
    public async Task reading_requests_need_no_marker()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task the_rule_does_not_apply_outside_the_api()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/app/clients");

        //Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task a_refused_request_never_reaches_the_controller_even_when_signed_in()
    {
        //Arrange
        using var client = _host.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderSignInHandler.HeaderName, "ada");

        //Act
        var refused = await client.SendAsync("POST", "/api/account/touch");
        var accepted = await client.SendAsync("POST", "/api/account/touch", withMarker: true);

        //Assert
        await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
