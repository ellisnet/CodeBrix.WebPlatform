using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Security;

public class RequestedWithTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RequestedWithTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("POST", "/api/clients")]
    [InlineData("PUT", "/api/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/time-entries/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/clients")]
    public async Task state_changing_requests_without_the_header_answer_400(string method, string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
        problem.TryGetProperty("errors", out _).Should().BeFalse();
    }

    [Fact]
    public void the_demanded_value_is_the_marker_the_backend_declares_for_its_frontend()
    {
        //Act
        var declared = WireRequestMarkerAttribute.GetValue(typeof(Program).Assembly);

        //Assert
        declared.Should().Be("WorkSite");
        WireRequestMarkerAttribute.HeaderName.Should().Be("X-Requested-With");
    }

    [Fact]
    public async Task a_wrong_header_value_is_refused_too()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        //Act
        var response = await client.SendAsync(request);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Fact]
    public async Task a_refused_request_changes_nothing()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        await client.PostAsync("/api/clients", new { name = "Sneaky", color = "red" }, withRequestedWith: false);

        //Assert
        (await ApiClient.JsonAsync(await client.GetAsync("/api/clients"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task get_requests_need_no_header()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.GetAsync("/api/clients");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task the_header_rule_does_not_apply_outside_the_api()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/app/clients"));

        //Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}
