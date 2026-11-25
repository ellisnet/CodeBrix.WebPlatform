using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>
/// The built frontend (frontend-built/) and the developer's own files (wwwroot/) served side by
/// side: what each folder serves, which one wins at an address both have
/// (PlatformBackendOptions.FilePrecedence), the start-up warning about such addresses, and the
/// files never served.
/// </summary>
public class BuiltFrontendAndWebRootTests
{
    private const string OwnIndex = "<!doctype html><html><body>a stray index of the developer</body></html>";

    private static readonly byte[] BuiltScript = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("export const built = 'built';\n", 100)));

    private static void WriteClashingFiles(TempWebRoot root)
    {
        root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" }));
        root.WriteWithCopies("index.html", Encoding.UTF8.GetBytes(TempWebRoot.DefaultIndex));
        root.WriteWithCopies("shared.js", BuiltScript);
        root.WriteOwn("index.html", OwnIndex);
        root.WriteOwn("shared.js", "export const own = 'own';");
        root.WriteOwn("robots.txt", "User-agent: *");
    }

    [Fact]
    public async Task the_built_frontend_and_the_web_roots_own_files_are_both_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
            root.WriteOwn("robots.txt", "User-agent: *");
            root.WriteOwn("images/logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        });
        using var client = host.CreateClient();

        //Act
        var index = await client.SendAsync("GET", "/");
        var deepLink = await client.SendAsync("GET", "/clients/7");
        var wasm = await client.SendAsync("GET", "/app.wasm");
        var robots = await client.SendAsync("GET", "/robots.txt");
        var logo = await client.SendAsync("GET", "/images/logo.svg");

        //Assert
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        await deepLink.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        wasm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await robots.TextAsync()).Should().Be("User-agent: *");
        logo.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Logs.Messages(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task without_a_web_root_folder_the_built_frontend_is_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" })),
            createWebRoot: false);
        using var client = host.CreateClient();

        //Act
        var index = await client.SendAsync("GET", "/");
        var named = await client.SendAsync("GET", "/sales/orders");
        var echo = await client.SendAsync("GET", "/api/echo");

        //Assert
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        await named.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
        echo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task by_default_the_built_frontend_wins_at_an_address_both_folders_have()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteClashingFiles);
        using var client = host.CreateClient();

        //Act
        var root = await client.SendAsync("GET", "/");
        var index = await client.SendAsync("GET", "/index.html");
        var deepLink = await client.SendAsync("GET", "/clients/7");
        var script = await client.SendWithAsync("GET", "/shared.js", ("Accept-Encoding", "br"));
        var robots = await client.SendAsync("GET", "/robots.txt");

        //Assert
        new PlatformBackendOptions().FilePrecedence.Should().Be(FrontendFilePrecedence.BuiltFrontendWins);
        await root.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        await deepLink.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        script.Coding().Should().Be("br");
        (await script.DecodedAsync()).Should().Equal(BuiltScript);
        (await robots.TextAsync()).Should().Be("User-agent: *");
    }

    [Fact]
    public async Task with_BuiltFrontendWins_one_warning_names_the_clashing_addresses()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteClashingFiles);

        //Act
        var warnings = host.Logs.Messages(LogLevel.Warning).ToArray();

        //Assert
        warnings.Should().ContainSingle().Which.Should().Be(
            "2 address(es) name a file in both frontend-built/ and wwwroot/; the built frontend's file is served and the wwwroot/ file is hidden (FilePrecedence is BuiltFrontendWins): /index.html, /shared.js.");
    }

    [Fact]
    public async Task with_WebRootWins_the_web_roots_file_wins_its_fallback_included_and_the_built_copies_are_not_used()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteClashingFiles,
            options => options.FilePrecedence = FrontendFilePrecedence.WebRootWins);
        using var client = host.CreateClient();

        //Act
        var root = await client.SendWithAsync("GET", "/", ("Accept-Encoding", "br, gzip"));
        var deepLink = await client.SendWithAsync("GET", "/clients/7", ("Accept-Encoding", "br, gzip"));
        var script = await client.SendWithAsync("GET", "/shared.js", ("Accept-Encoding", "br, gzip"));
        var named = await client.SendAsync("GET", "/sales/orders");
        var wasm = await client.SendAsync("GET", "/app.wasm");

        //Assert
        root.Coding().Should().BeNull();
        await root.ShouldBeIndexAsync(OwnIndex);
        deepLink.Coding().Should().BeNull();
        await deepLink.ShouldBeIndexAsync(OwnIndex);
        script.Coding().Should().BeNull();
        (await script.TextAsync()).Should().Be("export const own = 'own';");
        await named.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
        wasm.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task with_WebRootWins_one_warning_names_the_clashing_addresses_for_that_side()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteClashingFiles,
            options => options.FilePrecedence = FrontendFilePrecedence.WebRootWins);

        //Act
        var warnings = host.Logs.Messages(LogLevel.Warning).ToArray();

        //Assert
        warnings.Should().ContainSingle().Which.Should().Be(
            "2 address(es) name a file in both wwwroot/ and frontend-built/; the wwwroot/ file is served and the built frontend's is hidden (FilePrecedence is WebRootWins): /index.html, /shared.js.");
    }

    [Fact]
    public void UsePlatformBackend_refuses_a_FilePrecedence_that_is_not_a_value()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.FilePrecedence = (FrontendFilePrecedence)7);

        //Act
        System.Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<System.InvalidOperationException>().Which.Message.Should().Contain("FilePrecedence is 7");
    }

    [Theory]
    [InlineData("/.gitignore")]
    [InlineData("/.GITIGNORE")]
    [InlineData("/.gitignore.")]
    [InlineData("/codebrix-frontends.toml")]
    public async Task the_built_frontends_gitignore_and_manifest_are_never_served(string path)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
            root.WriteBuilt(".gitignore", "# Written by the frontend build.\n*\n");
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await response.TextAsync()).Should().NotContain("Written by");
        (await response.TextAsync()).Should().NotContain("written-by");
    }

    [Fact]
    public async Task a_file_of_the_manifests_name_in_the_web_root_is_not_served()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault, writeManifest: false);
            root.WriteOwn(FrontendManifest.FileName, "a file of the developer");
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/" + FrontendManifest.FileName);

        //Assert
        (await response.TextAsync()).Should().NotContain("a file of the developer");
    }

    [Fact]
    public async Task a_br_file_beside_a_web_root_file_is_not_a_copy()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
            root.WriteOwn("own.js", "export const own = 1;");
            root.WriteOwn("own.js.br", "not a brotli copy");
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/own.js", ("Accept-Encoding", "br"));

        //Assert
        response.Coding().Should().BeNull();
        (await response.TextAsync()).Should().Be("export const own = 1;");
    }
}
