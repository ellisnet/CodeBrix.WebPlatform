using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>Only named applications ("sales", "admin"), read from the manifest, with the developer's own root index.html.</summary>
public sealed class NamedApplicationsWithRootPageFixture : IAsyncLifetime
{
    public static readonly FrontendApplicationSet Applications = FrontendApplicationSet.FromNames(new[] { "sales", "admin" });

    public BackendHost Host { get; private set; }

    public async ValueTask InitializeAsync()
        => Host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(Applications);
            root.WriteOwn("index.html", TempWebRoot.OwnRootIndex);
            root.WriteOwn("robots.txt", "User-agent: *");
        });

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

/// <summary>The same applications without a root index.html.</summary>
public sealed class NamedApplicationsWithoutRootPageFixture : IAsyncLifetime
{
    public BackendHost Host { get; private set; }

    public async ValueTask InitializeAsync()
        => Host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(NamedApplicationsWithRootPageFixture.Applications));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

public class NamedApplicationsWithRootPageTests : IClassFixture<NamedApplicationsWithRootPageFixture>
{
    private readonly BackendHost _host;

    public NamedApplicationsWithRootPageTests(NamedApplicationsWithRootPageFixture fixture)
    {
        _host = fixture.Host;
    }

    [Fact]
    public async Task the_site_root_answers_the_developers_own_index_html()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/");

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.OwnRootIndex);
    }

    [Fact]
    public async Task HEAD_of_the_site_root_answers_too()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("HEAD", "/");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().Be(TempWebRoot.OwnRootIndex.Length);
    }

    [Theory]
    [InlineData("/features")]
    [InlineData("/app")]
    [InlineData("/app/clients")]
    [InlineData("/salesman")]
    [InlineData("/deep/link/1")]
    public async Task an_address_outside_every_application_is_404(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Theory]
    [InlineData("/sales", "sales")]
    [InlineData("/sales/orders/42", "sales")]
    [InlineData("/admin/", "admin")]
    [InlineData("/admin/users", "admin")]
    public async Task the_named_applications_are_served(string path, string application)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.NamedIndex(application));
    }

    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/sales/app.wasm")]
    [InlineData("/admin/assets/styles.css")]
    public async Task the_web_roots_files_are_served(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/no-such-route")]
    [InlineData("/api/sales")]
    public async Task unknown_api_addresses_answer_404_with_the_standard_body(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task a_POST_to_the_site_root_is_not_answered_with_the_index_html()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task the_manifest_is_not_served()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/codebrix-frontends.toml");

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }
}

public class NamedApplicationsWithoutRootPageTests : IClassFixture<NamedApplicationsWithoutRootPageFixture>
{
    private readonly BackendHost _host;

    public NamedApplicationsWithoutRootPageTests(NamedApplicationsWithoutRootPageFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/features")]
    public async Task the_site_root_and_every_address_outside_the_applications_is_404(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Theory]
    [InlineData("/sales/orders", "sales")]
    [InlineData("/admin", "admin")]
    public async Task the_named_applications_are_served(string path, string application)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.NamedIndex(application));
    }

    [Fact]
    public void the_start_up_log_names_the_applications_served()
    {
        //Act
        var messages = _host.Logs.Messages(LogLevel.Information);

        //Assert
        messages.Should().Contain("Serving the frontend applications sales at /sales, admin at /admin from frontend-built/.");
    }
}
