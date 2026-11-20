using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.SignIn;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>Before a frontend has been built: no manifest, and the API works.</summary>
public class NoManifestTests
{
    [Fact]
    public async Task with_an_empty_web_root_the_api_works()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderSignInHandler.HeaderName, "ada");

        //Act
        var echo = await client.SendAsync("GET", "/api/echo");
        var account = await client.SendAsync("GET", "/api/account");
        var unknown = await client.SendAsync("GET", "/api/no-such-route");

        //Assert
        echo.StatusCode.Should().Be(HttpStatusCode.OK);
        account.StatusCode.Should().Be(HttpStatusCode.OK);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app/clients")]
    [InlineData("/features")]
    public async Task with_an_empty_web_root_frontend_addresses_are_404(string path)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync();
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Fact]
    public async Task without_a_web_root_folder_the_api_works_and_nothing_else_is_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(createWebRoot: false);
        using var client = host.CreateClient();

        //Act
        var echo = await client.SendAsync("GET", "/api/echo");
        var root = await client.SendAsync("GET", "/");

        //Assert
        echo.StatusCode.Should().Be(HttpStatusCode.OK);
        await root.ShouldBePlainNotFoundAsync();
    }

    [Fact]
    public async Task built_files_without_a_manifest_get_no_fallback()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
            root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" }), writeManifest: false));
        using var client = host.CreateClient();

        //Act
        var deepLink = await client.SendAsync("GET", "/app/clients");
        var namedDeepLink = await client.SendAsync("GET", "/sales/orders");
        var wasm = await client.SendAsync("GET", "/sales/app.wasm");
        var root = await client.SendAsync("GET", "/");

        //Assert
        await deepLink.ShouldBePlainNotFoundAsync();
        await namedDeepLink.ShouldBePlainNotFoundAsync();
        wasm.StatusCode.Should().Be(HttpStatusCode.OK);
        await root.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task the_start_up_log_says_that_no_frontend_is_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync();

        //Act
        var messages = host.Logs.Messages(LogLevel.Information).ToArray();

        //Assert
        messages.Should().ContainSingle().Which.Should().Contain("No frontend manifest was found");
    }
}
