using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.SignIn;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>The standard error body for the answers the framework produces by itself, and the model-binding answer.</summary>
public class ProblemBodiesTests : IClassFixture<PlumbingFixture>
{
    private readonly BackendHost _host;

    public ProblemBodiesTests(PlumbingFixture fixture)
    {
        _host = fixture.Host;
    }

    [Fact]
    public async Task a_controllers_bare_404_gets_the_standard_body()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo/missing");

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task a_controllers_bare_409_gets_the_standard_body()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("PUT", "/api/echo/conflict", withMarker: true);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task a_body_that_is_not_json_answers_415_with_the_standard_body()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "text=x",
            contentType: "application/x-www-form-urlencoded");

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "validation_failed");
    }

    [Fact]
    public async Task a_5xx_is_left_alone()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo/unavailable");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.TextAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task an_answer_with_a_body_of_its_own_is_left_alone()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("DELETE", "/api/echo/own-problem", withMarker: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.TextAsync()).Should().Contain("own_code");
    }

    [Fact]
    public async Task an_authorize_controller_answers_an_empty_401_when_not_signed_in()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var get = await client.SendAsync("GET", "/api/account");
        var post = await client.SendAsync("POST", "/api/account/touch", withMarker: true);

        //Assert
        get.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await get.TextAsync()).Should().BeEmpty();
        post.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await post.TextAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task an_authorize_controller_answers_when_signed_in()
    {
        //Arrange
        using var client = _host.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderSignInHandler.HeaderName, "ada");

        //Act
        var response = await client.SendAsync("GET", "/api/account");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.TextAsync()).Should().Be("ada");
    }

    [Fact]
    public async Task malformed_json_answers_400_validation_failed()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "{ \"text\": ");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("title").GetString().Should().Be("One or more fields are invalid.");
        problem.ErrorFields().Should().NotBeEmpty();
    }

    [Fact]
    public async Task an_empty_body_is_reported_against_the_body_not_the_parameter_name()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("body");
        problem.GetProperty("errors").GetProperty("body")[0].GetString().Should().Be("The request body is not valid JSON.");
    }

    [Fact]
    public async Task a_value_of_the_wrong_type_is_reported_against_its_camel_case_field()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "{\"text\":\"a\",\"count\":\"many\"}");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("count");
        problem.GetProperty("errors").GetProperty("count")[0].GetString().Should().Be("The value is not valid.");
    }

    [Fact]
    public async Task a_query_value_that_cannot_be_read_is_reported_against_its_name()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo?count=abc");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("count");
    }

    [Fact]
    public async Task a_missing_required_field_is_reported_against_it()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "{\"count\":1}");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("text");
    }

    [Fact]
    public async Task a_404_outside_the_api_gets_no_body()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/missing.png");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.TextAsync()).Should().BeEmpty();
    }
}
