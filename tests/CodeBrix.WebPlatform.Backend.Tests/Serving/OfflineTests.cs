using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>
/// The offline file: while wwwroot/app-offline.html is there the backend answers 503 (the
/// file's content for pages, the standard error body for the API and hubs), still serves the
/// developer's other wwwroot/ files, and never serves the built frontend or the file itself;
/// putting the file there and removing it takes effect without a restart.
/// </summary>
public class OfflineTests
{
    private const string OfflinePage = "<!doctype html><html><body>Back soon.</body></html>";

    private static void WriteFiles(TempWebRoot root)
    {
        root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" }));
        root.WriteOwn("offline.css", "body { color: gray; }");
        root.WriteOwn("robots.txt", "User-agent: *");
    }

    [Fact]
    public async Task the_offline_file_takes_the_backend_offline_and_back_without_a_restart()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var before = await client.SendAsync("GET", "/");
        host.WebRoot.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        var offline = await client.SendAsync("GET", "/");
        host.WebRoot.DeleteOwn(PlatformBackendPaths.OfflineFileName);
        var after = await client.SendAsync("GET", "/");

        //Assert
        await before.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        offline.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await offline.TextAsync()).Should().Be(OfflinePage);
        await after.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/clients/7")]
    [InlineData("/sales")]
    [InlineData("/sales/orders")]
    [InlineData("/sales/index.html")]
    [InlineData("/app.wasm")]
    [InlineData("/sales/assets/styles.css")]
    [InlineData("/status")]
    [InlineData("/" + PlatformBackendPaths.OfflineFileName)]
    [InlineData("/APP-OFFLINE.HTML")]
    public async Task while_offline_pages_built_files_and_the_offline_file_itself_answer_the_offline_page(string path)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType.ToString().Should().Be("text/html; charset=utf-8");
        response.Header("Cache-Control").Should().Be("no-store");
        (await response.TextAsync()).Should().Be(OfflinePage);
    }

    [Fact]
    public async Task while_offline_a_HEAD_gets_the_offline_headers_without_a_body()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("HEAD", "/");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentLength.Should().Be(OfflinePage.Length);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET", "/api/echo", false, null)]
    [InlineData("POST", "/api/echo", false, null)]
    [InlineData("GET", "/api/no-such-route", false, null)]
    [InlineData("GET", "/api/account", false, null)]
    [InlineData("POST", "/api/hubs/echo/negotiate?negotiateVersion=1", true, BackendHost.OwnOrigin)]
    [InlineData("POST", "/api/hubs/echo/negotiate?negotiateVersion=1", true, "https://elsewhere.example")]
    public async Task while_offline_api_and_hub_requests_answer_503_with_the_standard_error_body(string method, string path, bool withMarker, string origin)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path, withMarker: withMarker, origin: origin, json: method == "POST" ? "{\"text\":\"a\"}" : null);

        //Assert
        var body = await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, ApiProblemCodes.Offline);
        body.GetProperty("title").GetString().Should().Be(ApiProblems.OfflineTitle);
        response.Header("Cache-Control").Should().Be("no-store");
    }

    [Fact]
    public async Task while_offline_the_developers_other_files_are_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });
        using var client = host.CreateClient();

        //Act
        var stylesheet = await client.SendAsync("GET", "/offline.css");
        var robots = await client.SendAsync("GET", "/robots.txt");

        //Assert
        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stylesheet.TextAsync()).Should().Be("body { color: gray; }");
        stylesheet.Content.Headers.ContentType.ToString().Should().Be("text/css; charset=utf-8");
        stylesheet.Header("Cache-Control").Should().Be("no-cache");
        (await robots.TextAsync()).Should().Be("User-agent: *");
    }

    [Fact]
    public async Task while_offline_a_web_root_file_hidden_by_the_built_frontend_is_served_and_the_built_one_is_not()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn("app.wasm", "own wasm");
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/app.wasm");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be("own wasm");
    }

    [Fact]
    public async Task while_online_the_offline_file_is_not_served_at_its_address()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/" + PlatformBackendPaths.OfflineFileName);

        //Assert
        (await response.TextAsync()).Should().NotBe(OfflinePage);
        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task the_offline_file_works_when_the_web_root_did_not_exist_at_start_up()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            createWebRoot: false);
        using var client = host.CreateClient();

        //Act
        var before = await client.SendAsync("GET", "/");
        host.WebRoot.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        var offline = await client.SendAsync("GET", "/");
        var api = await client.SendAsync("GET", "/api/echo");

        //Assert
        Directory.Exists(host.WebRoot.Path).Should().BeTrue();
        await before.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        offline.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await offline.TextAsync()).Should().Be(OfflinePage);
        await api.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, ApiProblemCodes.Offline);
    }

    [Fact]
    public async Task the_offline_file_works_with_the_frontend_applications_switched_off()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        }, options => options.ServeFrontendApplications = false);
        using var client = host.CreateClient();

        //Act
        var page = await client.SendAsync("GET", "/");
        var api = await client.SendAsync("GET", "/api/echo");

        //Assert
        page.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        api.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task an_offline_start_is_named_in_a_warning()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            WriteFiles(root);
            root.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        });

        //Act
        var warnings = host.Logs.Messages(LogLevel.Warning).ToArray();

        //Assert
        warnings.Should().ContainSingle().Which.Should().Be(
            "The offline file app-offline.html is in the web root, so the backend answers 503 until it is removed.");
    }
}
