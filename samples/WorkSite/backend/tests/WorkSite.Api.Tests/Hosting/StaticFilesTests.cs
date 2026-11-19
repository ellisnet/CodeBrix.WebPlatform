using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hosting;

public class StaticFilesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StaticFilesTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/features")]
    [InlineData("/sign-in")]
    [InlineData("/app")]
    [InlineData("/app/clients")]
    [InlineData("/app/reports")]
    public async Task app_routes_are_served_index_html(string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync(path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("<div id=\"main\">");
    }

    [Fact]
    public async Task wasm_files_are_served_as_application_wasm()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/app.wasm");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/wasm");
    }

    [Fact]
    public async Task a_missing_file_with_an_extension_is_404_not_index_html()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/assets/missing.js");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/no-such-route")]
    [InlineData("/api")]
    [InlineData("/api/clients/not-a-guid")]
    [InlineData("/api/app/clients")]
    [InlineData("/api/reports/export.pdf")]
    public async Task unknown_api_routes_are_404_with_the_contract_body(string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync(path);

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        problem.GetRawText().Should().NotContain("<html");
    }

    [Fact]
    public async Task unknown_api_routes_answer_404_for_other_methods_too()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/no-such-route", new { });

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }
}
