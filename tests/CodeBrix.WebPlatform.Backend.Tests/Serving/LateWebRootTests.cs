using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.FileProviders;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>
/// A web root (wwwroot/) that did not exist when the backend started: the developer's files are
/// served as soon as the folder exists, without a restart, also after it is deleted and created
/// again; its hidden names stay hidden. A web root that existed at start-up works as before.
/// </summary>
public class LateWebRootTests
{
    private const string OfflinePage = "<!doctype html><html><head><link rel=\"stylesheet\" href=\"/offline.css\"></head><body>Back soon.</body></html>";

    private static Task<BackendHost> StartWithoutWebRootAsync()
        => BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault), createWebRoot: false);

    [Fact]
    public async Task a_web_root_created_after_start_up_is_served_without_a_restart()
    {
        //Arrange
        await using var host = await StartWithoutWebRootAsync();
        using var client = host.CreateClient();

        //Act
        var before = await client.SendAsync("GET", "/notes.txt");
        host.WebRoot.WriteOwn("notes.txt", "User-agent: *");
        host.WebRoot.WriteOwn("images/logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var robots = await client.SendAsync("GET", "/notes.txt");
        var logo = await client.SendAsync("GET", "/images/logo.svg");
        var index = await client.SendAsync("GET", "/");

        //Assert
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);
        robots.StatusCode.Should().Be(HttpStatusCode.OK);
        (await robots.TextAsync()).Should().Be("User-agent: *");
        robots.Content.Headers.ContentType.ToString().Should().Be("text/plain; charset=utf-8");
        robots.Header("Cache-Control").Should().Be("no-cache");
        logo.StatusCode.Should().Be(HttpStatusCode.OK);
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task an_offline_page_and_its_stylesheet_in_a_new_web_root_are_served()
    {
        //Arrange
        await using var host = await StartWithoutWebRootAsync();
        using var client = host.CreateClient();

        //Act
        host.WebRoot.WriteOwn("offline.css", "body { color: gray; }");
        host.WebRoot.WriteOwn(PlatformBackendPaths.OfflineFileName, OfflinePage);
        var page = await client.SendAsync("GET", "/");
        var stylesheet = await client.SendAsync("GET", "/offline.css");
        var itself = await client.SendAsync("GET", "/" + PlatformBackendPaths.OfflineFileName);

        //Assert
        page.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await page.TextAsync()).Should().Be(OfflinePage);
        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stylesheet.TextAsync()).Should().Be("body { color: gray; }");
        itself.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task a_deleted_web_root_answers_404_and_a_recreated_one_is_served_again()
    {
        //Arrange
        await using var host = await StartWithoutWebRootAsync();
        using var client = host.CreateClient();
        host.WebRoot.WriteOwn("notes.txt", "User-agent: *");
        var first = await client.SendAsync("GET", "/notes.txt");

        //Act
        host.WebRoot.DeleteWebRoot();
        var deleted = await client.SendAsync("GET", "/notes.txt");
        var index = await client.SendAsync("GET", "/");
        host.WebRoot.WriteOwn("notes.txt", "User-agent: again");
        var recreated = await client.SendAsync("GET", "/notes.txt");

        //Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        (await recreated.TextAsync()).Should().Be("User-agent: again");
    }

    [Fact]
    public async Task the_hidden_names_stay_hidden_in_a_web_root_created_after_start_up()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault, writeManifest: false),
            createWebRoot: false);
        using var client = host.CreateClient();

        //Act
        host.WebRoot.WriteOwn(FrontendManifest.FileName, "a file of the developer");
        var manifest = await client.SendAsync("GET", "/" + FrontendManifest.FileName);

        //Assert
        (await manifest.TextAsync()).Should().NotContain("a file of the developer");
    }

    [Fact]
    public async Task a_built_file_still_wins_over_a_file_of_a_web_root_created_after_start_up()
    {
        //Arrange
        await using var host = await StartWithoutWebRootAsync();
        using var client = host.CreateClient();

        //Act
        host.WebRoot.WriteOwn("index.html", "the developer's index");
        var index = await client.SendAsync("GET", "/index.html");

        //Assert
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task a_web_root_that_existed_at_start_up_is_read_through_the_hosts_provider_as_before()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault));
        using var client = host.CreateClient();

        //Act
        host.WebRoot.WriteOwn("notes.txt", "User-agent: *");
        var added = await client.SendAsync("GET", "/notes.txt");
        host.WebRoot.DeleteWebRoot();
        var deleted = await client.SendAsync("GET", "/notes.txt");
        host.WebRoot.WriteOwn("notes.txt", "User-agent: again");
        var recreated = await client.SendAsync("GET", "/notes.txt");

        //Assert
        host.App.Environment.WebRootFileProvider.Should().BeOfType<PhysicalFileProvider>();
        (await added.TextAsync()).Should().Be("User-agent: *");
        deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await recreated.TextAsync()).Should().Be("User-agent: again");
        host.Logs.Messages(Microsoft.Extensions.Logging.LogLevel.Warning).Should().BeEmpty();
    }
}
