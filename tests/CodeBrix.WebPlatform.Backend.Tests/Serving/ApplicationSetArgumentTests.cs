using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Applications;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>UsePlatformBackend(applications): the set given in code is served and the manifest is not read.</summary>
public class ApplicationSetArgumentTests
{
    [Fact]
    public async Task the_set_given_is_served_without_a_manifest()
    {
        //Arrange
        var applications = FrontendApplicationSet.FromNames(new[] { null, "sales" });
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(applications, writeManifest: false),
            applications: applications);
        using var client = host.CreateClient();

        //Act
        var deepLink = await client.SendAsync("GET", "/app/clients");
        var namedDeepLink = await client.SendAsync("GET", "/sales/orders");

        //Assert
        await deepLink.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        await namedDeepLink.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
    }

    [Fact]
    public async Task the_set_given_wins_over_the_manifest()
    {
        //Arrange
        var built = FrontendApplicationSet.FromNames(new[] { null, "sales" });
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(built),
            applications: FrontendApplicationSet.FromNames(new[] { "sales" }));
        using var client = host.CreateClient();

        //Act
        var deepLink = await client.SendAsync("GET", "/app/clients");
        var namedDeepLink = await client.SendAsync("GET", "/sales/orders");

        //Assert
        await deepLink.ShouldBePlainNotFoundAsync();
        await namedDeepLink.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
    }

    [Fact]
    public async Task a_broken_manifest_is_not_read_when_a_set_is_given()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
            {
                root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault, writeManifest: false);
                root.Write(FrontendManifest.FileName, "this is not a manifest");
            },
            applications: FrontendApplicationSet.SingleDefault);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/");

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task an_application_without_its_index_html_answers_404_and_is_named_in_a_warning()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(applications: FrontendApplicationSet.FromNames(new[] { "sales" }));
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/sales/orders");

        //Assert
        await response.ShouldBePlainNotFoundAsync();
        host.Logs.Messages(LogLevel.Warning).Should().ContainSingle()
            .Which.Should().Be("The frontend application sales has no sales/index.html in the web root, so its addresses answer 404; build the frontend into the web root.");
    }

    [Fact]
    public void UsePlatformBackend_refuses_a_null_set()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root);

        //Act
        Action act = () => app.UsePlatformBackend(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void a_broken_manifest_stops_the_start_up_with_the_readers_message()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.Write(FrontendManifest.FileName, "written-by = \"someone else\"\nformat = 1\n");
        using var app = BackendHost.Build(root);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<FrontendManifestException>()
            .Which.FilePath.Should().Be(FrontendManifestReader.GetPath(root.Path));
    }

    [Fact]
    public void a_broken_manifest_is_not_read_when_serving_is_switched_off()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.Write(FrontendManifest.FileName, "nonsense");
        using var app = BackendHost.Build(root, options => options.ServeFrontendApplications = false);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void a_set_is_refused_when_serving_is_switched_off()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.ServeFrontendApplications = false);

        //Act
        Action act = () => app.UsePlatformBackend(FrontendApplicationSet.SingleDefault);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ServeFrontendApplications is switched off*");
    }

    [Fact]
    public async Task the_manifest_is_hidden_when_a_set_is_given_too()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            applications: FrontendApplicationSet.SingleDefault);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/codebrix-frontends.toml");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Logs.Messages(LogLevel.Warning).Any().Should().BeFalse();
    }
}
