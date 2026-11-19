using System.Collections.Generic;
using System.Text.Json;
using CodeBrix.WebPlatform.Core.Problems;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Problems;

public class ApiProblemTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void content_type_is_problem_json()
        => ApiProblem.ContentType.Should().Be("application/problem+json");

    [Fact]
    public void a_problem_without_field_messages_leaves_errors_out_of_the_body()
    {
        //Arrange
        var problem = new ApiProblem { Title = "That item could not be found.", Status = 404, Code = ApiProblemCodes.NotFound };

        //Act
        var json = JsonSerializer.Serialize(problem, Web);

        //Assert
        json.Should().Be("""{"title":"That item could not be found.","status":404,"code":"not_found"}""");
    }

    [Fact]
    public void field_messages_are_written_under_errors()
    {
        //Arrange
        var problem = new ApiProblem
        {
            Title = "One or more fields are invalid.",
            Status = 400,
            Code = ApiProblemCodes.ValidationFailed,
            Errors = new Dictionary<string, string[]> { ["email"] = ["Enter a valid email address."] },
        };

        //Act
        var json = JsonSerializer.Serialize(problem, Web);

        //Assert
        json.Should().Be("""{"title":"One or more fields are invalid.","status":400,"code":"validation_failed","errors":{"email":["Enter a valid email address."]}}""");
    }

    [Fact]
    public void a_body_reads_back_into_the_same_values()
    {
        //Act
        var problem = JsonSerializer.Deserialize<ApiProblem>(
            """{"title":"Name taken.","status":409,"code":"conflict","errors":{"name":["That name is already used."]}}""", Web);

        //Assert
        problem.Title.Should().Be("Name taken.");
        problem.Status.Should().Be(409);
        problem.Code.Should().Be(ApiProblemCodes.Conflict);
        problem.Errors["name"].Should().Equal("That name is already used.");
    }

    [Fact]
    public void the_platform_codes_are_the_ones_the_frontend_assumes()
    {
        //Assert
        ApiProblemCodes.ValidationFailed.Should().Be("validation_failed");
        ApiProblemCodes.MissingRequestHeader.Should().Be("missing_request_header");
        ApiProblemCodes.InvalidCredentials.Should().Be("invalid_credentials");
        ApiProblemCodes.NotFound.Should().Be("not_found");
        ApiProblemCodes.Conflict.Should().Be("conflict");
        ApiProblemCodes.RateLimited.Should().Be("rate_limited");
        ApiProblemCodes.ServerError.Should().Be("server_error");
    }
}
