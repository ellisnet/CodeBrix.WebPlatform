using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>Each piece switched off on its own, with the others still on.</summary>
public class SwitchedOffTests
{
    [Fact]
    public async Task without_the_request_marker_check_a_change_needs_no_header_and_no_marker_is_demanded()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.RequireRequestMarker = false,
            applicationName: typeof(SwitchedOffTests).Assembly.GetName().Name);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", json: "{\"text\":\"a\",\"count\":1}");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task without_the_hub_origin_check_a_hub_accepts_any_origin()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.RequireHubOrigin = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/hubs/echo/negotiate?negotiateVersion=1", withMarker: true,
            origin: "https://evil.example");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task without_problem_bodies_a_bare_404_is_not_given_the_standard_body()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.AddProblemBodiesToStatusCodes = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo/missing");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.TextAsync()).Should().NotContain("\"code\"");
    }

    [Fact]
    public async Task without_problem_bodies_unknown_api_routes_still_get_the_standard_body()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.AddProblemBodiesToStatusCodes = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/no-such-route");

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task without_the_model_binding_answer_MVC_answers_with_its_own_problem_details()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.AnswerModelBindingFailures = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "{ \"text\": ");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = JsonDocument.Parse(await response.TextAsync()).RootElement;
        body.TryGetProperty("code", out _).Should().BeFalse();
        body.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task without_the_unknown_api_answer_an_unknown_api_address_is_still_404_with_the_standard_body()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options => options.AnswerUnknownApiRoutes = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/no-such-route");

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData("GET", "/api/no-such-route")]
    [InlineData("GET", "/api")]
    [InlineData("POST", "/api/no-such-route")]
    public async Task with_both_api_answers_off_an_api_address_is_never_answered_with_the_index_html(string method, string path)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options =>
            {
                options.AnswerUnknownApiRoutes = false;
                options.AddProblemBodiesToStatusCodes = false;
            });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path, withMarker: true);

        //Assert
        ((int)response.StatusCode).Should().BeOneOf(404, 405);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app/clients")]
    [InlineData("/app.wasm")]
    [InlineData("/index.html")]
    public async Task without_frontend_serving_no_file_and_no_fallback_is_served(string path)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options => options.ServeFrontendApplications = false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Fact]
    public async Task without_frontend_serving_the_api_works()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.ServeFrontendApplications = false);
        using var client = host.CreateClient();

        //Act
        var echo = await client.SendAsync("GET", "/api/echo");
        var unknown = await client.SendAsync("GET", "/api/no-such-route");

        //Assert
        echo.StatusCode.Should().Be(HttpStatusCode.OK);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task with_every_piece_off_the_backends_own_lines_still_work()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options =>
            {
                options.ServeFrontendApplications = false;
                options.AnswerUnknownApiRoutes = false;
                options.AddProblemBodiesToStatusCodes = false;
                options.AnswerModelBindingFailures = false;
                options.RequireRequestMarker = false;
                options.RequireHubOrigin = false;
                options.SendSecurityHeaders = false;
            });
        using var client = host.CreateClient();

        //Act
        var echo = await client.SendAsync("POST", "/api/echo", json: "{\"text\":\"a\",\"count\":1}");
        var account = await client.SendAsync("GET", "/api/account");
        var root = await client.SendAsync("GET", "/");

        //Assert
        echo.StatusCode.Should().Be(HttpStatusCode.OK);
        account.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        root.StatusCode.Should().Be(HttpStatusCode.NotFound);
        echo.Header("X-Frame-Options").Should().BeNull();
    }
}
