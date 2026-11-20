using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.Logging;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>A default application at the site root and two named ones, "sales" and "admin", read from the manifest.</summary>
public sealed class DefaultAndNamedApplicationsFixture : IAsyncLifetime
{
    public static readonly FrontendApplicationSet Applications = FrontendApplicationSet.FromNames(new[] { null, "sales", "admin" });

    public BackendHost Host { get; private set; }

    public async ValueTask InitializeAsync()
        => Host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(Applications));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

public class DefaultAndNamedApplicationsTests : IClassFixture<DefaultAndNamedApplicationsFixture>
{
    private readonly BackendHost _host;

    public DefaultAndNamedApplicationsTests(DefaultAndNamedApplicationsFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/features")]
    [InlineData("/app")]
    [InlineData("/app/clients/42")]
    [InlineData("/salesman")]
    [InlineData("/administration/users")]
    [InlineData("/apis")]
    public async Task addresses_outside_the_named_applications_answer_the_default_index_html(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Theory]
    [InlineData("/sales", "sales")]
    [InlineData("/sales/", "sales")]
    [InlineData("/sales/orders", "sales")]
    [InlineData("/sales/orders/42/lines", "sales")]
    [InlineData("/admin", "admin")]
    [InlineData("/admin/users/7", "admin")]
    public async Task addresses_of_a_named_application_answer_its_own_index_html(string path, string application)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.NamedIndex(application));
    }

    [Theory]
    [InlineData("/app.wasm")]
    [InlineData("/sales/app.wasm")]
    [InlineData("/admin/app.wasm")]
    public async Task wasm_files_are_served_as_application_wasm(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/wasm");
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().Equal(TempWebRoot.WasmBytes);
    }

    [Theory]
    [InlineData("/assets/styles.css")]
    [InlineData("/sales/assets/styles.css")]
    public async Task stylesheets_are_served_as_css(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/css");
    }

    [Theory]
    [InlineData("/assets/missing.js")]
    [InlineData("/sales/assets/missing.js")]
    [InlineData("/admin/report.pdf")]
    public async Task a_missing_file_is_404_not_an_index_html(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Theory]
    [InlineData("/", "default")]
    [InlineData("/sales/orders", "sales")]
    public async Task HEAD_of_a_frontend_address_answers_like_GET_without_a_body(string path, string application)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("HEAD", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        response.Content.Headers.ContentLength.Should().Be(
            (application == "default" ? TempWebRoot.DefaultIndex : TempWebRoot.NamedIndex(application)).Length);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("POST", "/features")]
    [InlineData("PUT", "/sales/orders")]
    [InlineData("DELETE", "/admin")]
    [InlineData("POST", "/")]
    public async Task a_state_changing_request_to_a_frontend_address_is_never_answered_with_an_index_html(string method, string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path, withMarker: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET", "/api")]
    [InlineData("GET", "/api/no-such-route")]
    [InlineData("GET", "/api/sales/orders")]
    [InlineData("GET", "/API/Whatever")]
    [InlineData("GET", "/api/echo/not-a-guid")]
    [InlineData("GET", "/api/report.pdf")]
    [InlineData("HEAD", "/api/no-such-route")]
    [InlineData("POST", "/api/no-such-route")]
    [InlineData("PUT", "/api/echo")]
    [InlineData("PATCH", "/api/no-such-route")]
    [InlineData("DELETE", "/api/no-such-route")]
    [InlineData("OPTIONS", "/api/no-such-route")]
    public async Task unknown_api_addresses_answer_404_with_the_standard_body_for_every_method(string method, string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync(method, path, withMarker: true);

        //Assert
        if (method == "HEAD")
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType.MediaType.Should().Be("application/problem+json");
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }
    }

    [Theory]
    [InlineData("/codebrix-frontends.toml")]
    [InlineData("/CODEBRIX-FRONTENDS.TOML")]
    [InlineData("/Codebrix-Frontends.toml")]
    [InlineData("//codebrix-frontends.toml")]
    [InlineData("/sales/../codebrix-frontends.toml")]
    [InlineData("/codebrix-frontends.toml.")]
    public async Task the_manifest_is_never_served(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var get = await client.SendAsync("GET", path);
        var head = await client.SendAsync("HEAD", path);

        //Assert
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await get.TextAsync()).Should().NotContain("written-by");
        head.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task controllers_win_over_the_default_applications_fallback()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/status");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be(SiteStatusController.Answer);
    }

    [Fact]
    public async Task controllers_win_over_a_named_applications_fallback()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/sales/status");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be(SiteStatusController.Answer);
    }

    [Fact]
    public async Task the_api_is_answered_by_its_controllers()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo?count=3");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be("{\"text\":\"echo\",\"count\":3}");
    }

    [Fact]
    public void the_start_up_log_names_the_applications_served()
    {
        //Act
        var messages = _host.Logs.Messages(LogLevel.Information);

        //Assert
        messages.Should().Contain("Serving the frontend applications app at /, sales at /sales, admin at /admin.");
        _host.Logs.Messages(LogLevel.Warning).Should().BeEmpty();
    }
}
