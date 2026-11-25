using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>
/// The backend's own lines after the pipeline call, with and without its own UseRouting: static
/// files come before routing, fallbacks never win over controllers or hubs, and an [Authorize]
/// controller still answers an empty 401.
/// </summary>
public class PipelineOrderTests
{
    public static readonly TheoryData<bool> RoutingChoices = new() { true, false };

    [Theory]
    [MemberData(nameof(RoutingChoices))]
    public async Task static_files_fallbacks_controllers_and_hubs_each_answer_their_own_addresses(bool backendCallsUseRouting)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(
            root => root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" })),
            useRouting: backendCallsUseRouting);
        using var client = host.CreateClient();

        //Act
        var wasm = await client.SendAsync("GET", "/sales/app.wasm");
        var deepLink = await client.SendAsync("GET", "/sales/orders");
        var controller = await client.SendAsync("GET", "/status");
        var namedController = await client.SendAsync("GET", "/sales/status");
        var api = await client.SendAsync("GET", "/api/echo");
        var hub = await client.SendAsync("POST", "/api/hubs/echo/negotiate?negotiateVersion=1", withMarker: true, origin: BackendHost.OwnOrigin);
        var signedOut = await client.SendAsync("GET", "/api/account");
        var unknown = await client.SendAsync("GET", "/api/nothing-here");

        //Assert
        wasm.Content.Headers.ContentType.MediaType.Should().Be("application/wasm");
        await deepLink.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
        (await controller.TextAsync()).Should().Be(SiteStatusController.Answer);
        (await namedController.TextAsync()).Should().Be(SiteStatusController.Answer);
        api.StatusCode.Should().Be(HttpStatusCode.OK);
        hub.StatusCode.Should().Be(HttpStatusCode.OK);
        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await signedOut.TextAsync()).Should().BeEmpty();
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task a_file_of_the_web_root_wins_over_a_controller_at_the_same_address()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteOwn("robots.txt", "User-agent: *"));
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/robots.txt");

        //Assert
        (await response.TextAsync()).Should().Be("User-agent: *");
    }

    [Fact]
    public async Task without_the_backends_own_UseRouting_the_host_routes_first_and_the_controller_wins()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteOwn("robots.txt", "User-agent: *"), useRouting: false);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/robots.txt");

        //Assert
        (await response.TextAsync()).Should().Be(SiteStatusController.Answer);
    }
}
