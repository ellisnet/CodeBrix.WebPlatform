using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>Only the default application, read from the manifest: a platform site as it always was.</summary>
public sealed class DefaultApplicationOnlyFixture : IAsyncLifetime
{
    public BackendHost Host { get; private set; }

    public async ValueTask InitializeAsync()
        => Host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

public class DefaultApplicationOnlyTests : IClassFixture<DefaultApplicationOnlyFixture>
{
    private readonly BackendHost _host;

    public DefaultApplicationOnlyTests(DefaultApplicationOnlyFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/features")]
    [InlineData("/sign-in")]
    [InlineData("/app")]
    [InlineData("/app/clients")]
    [InlineData("/sales/orders")]
    [InlineData("/a/very/deep/link/7")]
    public async Task every_address_that_is_not_a_file_answers_the_index_html(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task the_index_html_itself_is_served()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/index.html");

        //Assert
        await response.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
    }

    [Fact]
    public async Task wasm_files_are_served_as_application_wasm()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/app.wasm");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/wasm");
    }

    [Fact]
    public async Task a_stylesheet_is_served()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/assets/styles.css");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be("body { margin: 0; }");
    }

    [Fact]
    public async Task a_missing_file_with_an_extension_is_404_not_index_html()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/assets/missing.js");

        //Assert
        await response.ShouldBePlainNotFoundAsync();
    }

    [Fact]
    public async Task HEAD_of_a_deep_link_answers_like_GET_without_a_body()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("HEAD", "/app/clients");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task a_POST_to_a_frontend_address_is_not_answered_with_the_index_html()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/app/clients");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/no-such-route")]
    [InlineData("/api/app/clients")]
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
