using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Problems;

public class ApiProblemWriterTests
{
    [Fact]
    public async Task WriteAsync_sets_the_status_and_writes_camel_case_problem_json()
    {
        //Arrange
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        //Act
        await ApiProblemWriter.WriteAsync(context, ApiProblems.Validation("email", "Enter a valid email."));

        //Assert
        context.Response.StatusCode.Should().Be(400);
        context.Response.ContentType.Should().Be("application/problem+json");
        Encoding.UTF8.GetString(body.ToArray()).Should().Be(
            "{\"title\":\"One or more fields are invalid.\",\"status\":400,\"code\":\"validation_failed\",\"errors\":{\"email\":[\"Enter a valid email.\"]}}");
    }

    [Fact]
    public async Task WriteAsync_leaves_errors_out_when_there_are_none()
    {
        //Arrange
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        //Act
        await ApiProblemWriter.WriteAsync(context, ApiProblems.NotFound());

        //Assert
        Encoding.UTF8.GetString(body.ToArray()).Should().NotContain("errors");
    }

    [Fact]
    public async Task WriteAsync_refuses_null_arguments()
    {
        //Act
        Func<Task> noContext = () => ApiProblemWriter.WriteAsync(null, new ApiProblem());
        Func<Task> noProblem = () => ApiProblemWriter.WriteAsync(new DefaultHttpContext(), null);

        //Assert
        await noContext.Should().ThrowAsync<ArgumentNullException>();
        await noProblem.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WriteAsync_refuses_an_answer_that_has_already_started()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());

        //Act
        Func<Task> act = () => ApiProblemWriter.WriteAsync(context, ApiProblems.NotFound());

        //Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already started*");
    }

    [Fact]
    public void ApiProblemResult_carries_the_status_and_the_content_type()
    {
        //Arrange
        var problem = ApiProblems.Conflict("Taken.");

        //Act
        var result = new ApiProblemResult(problem);

        //Assert
        result.StatusCode.Should().Be(409);
        result.Value.Should().BeSameAs(problem);
        result.Problem.Should().BeSameAs(problem);
        result.ContentTypes.Should().Equal("application/problem+json");
        result.DeclaredType.Should().Be(typeof(ApiProblem));
    }

    [Fact]
    public void ApiProblemResult_refuses_a_null_problem()
    {
        //Act
        Action act = () => _ = new ApiProblemResult(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
