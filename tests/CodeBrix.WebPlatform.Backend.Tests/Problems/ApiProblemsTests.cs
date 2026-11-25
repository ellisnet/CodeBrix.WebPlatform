using System;
using System.Collections.Generic;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Problems;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Problems;

public class ApiProblemsTests
{
    [Fact]
    public void Validation_carries_the_messages_per_field()
    {
        //Arrange
        var errors = new Dictionary<string, string[]> { ["email"] = new[] { "Enter a valid email." } };

        //Act
        var problem = ApiProblems.Validation(errors);

        //Assert
        problem.Status.Should().Be(400);
        problem.Code.Should().Be("validation_failed");
        problem.Title.Should().Be(ApiProblems.ValidationTitle);
        problem.Errors.Should().BeSameAs(errors);
    }

    [Fact]
    public void Validation_with_one_field_carries_that_one_message()
    {
        //Act
        var problem = ApiProblems.Validation("name", "Enter a name.");

        //Assert
        problem.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("Enter a name.");
    }

    [Fact]
    public void Validation_refuses_null_errors()
    {
        //Act
        Action act = () => ApiProblems.Validation(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null, "message")]
    [InlineData("field", " ")]
    public void Validation_refuses_a_blank_field_or_message(string field, string message)
    {
        //Act
        Action act = () => ApiProblems.Validation(field, message);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MissingRequestHeader_is_400_with_a_neutral_title_and_no_errors()
    {
        //Act
        var problem = ApiProblems.MissingRequestHeader();

        //Assert
        problem.Status.Should().Be(400);
        problem.Code.Should().Be("missing_request_header");
        problem.Title.Should().Be("The request was refused because it did not come from this site.");
        problem.Errors.Should().BeNull();
    }

    [Fact]
    public void Forbidden_is_403_forbidden_with_the_sentence_given()
    {
        //Act
        var standard = ApiProblems.Forbidden();
        var own = ApiProblems.Forbidden("Only the owner can remove an account.");

        //Assert
        standard.Status.Should().Be(403);
        standard.Code.Should().Be("forbidden");
        standard.Title.Should().Be("You are not allowed to do that.");
        own.Title.Should().Be("Only the owner can remove an account.");
        own.Code.Should().Be(ApiProblemCodes.Forbidden);
    }

    [Fact]
    public void Offline_is_503_offline()
    {
        //Act
        var problem = ApiProblems.Offline();

        //Assert
        problem.Status.Should().Be(503);
        problem.Code.Should().Be("offline");
        problem.Title.Should().Be(ApiProblems.OfflineTitle);
    }

    [Fact]
    public void ForbiddenOrigin_is_403_forbidden_origin()
    {
        //Act
        var problem = ApiProblems.ForbiddenOrigin();

        //Assert
        problem.Status.Should().Be(403);
        problem.Code.Should().Be("forbidden_origin");
        problem.Title.Should().EndWith("did not come from this site.");
    }

    [Theory]
    [InlineData(null, ApiProblems.DefaultNotFoundTitle)]
    [InlineData("", ApiProblems.DefaultNotFoundTitle)]
    [InlineData("That client is gone.", "That client is gone.")]
    public void NotFound_is_404_with_the_title_given_or_the_default(string title, string expected)
    {
        //Act
        var problem = ApiProblems.NotFound(title);

        //Assert
        problem.Status.Should().Be(404);
        problem.Code.Should().Be("not_found");
        problem.Title.Should().Be(expected);
    }

    [Fact]
    public void Conflict_with_one_field_carries_its_message()
    {
        //Act
        var problem = ApiProblems.Conflict("That name is taken.", "name", "Choose another name.");

        //Assert
        problem.Status.Should().Be(409);
        problem.Code.Should().Be("conflict");
        problem.Title.Should().Be("That name is taken.");
        problem.Errors["name"].Should().Equal("Choose another name.");
    }

    [Fact]
    public void Conflict_without_errors_has_none()
        => ApiProblems.Conflict("The project still has entries.").Errors.Should().BeNull();

    [Fact]
    public void Conflict_refuses_a_blank_title()
    {
        //Act
        Action act = () => ApiProblems.Conflict(" ");

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RateLimited_is_429_rate_limited()
    {
        //Act
        var problem = ApiProblems.RateLimited();

        //Assert
        problem.Status.Should().Be(429);
        problem.Code.Should().Be("rate_limited");
    }

    [Theory]
    [InlineData(404, "not_found", ApiProblems.NoSuchApiRouteTitle)]
    [InlineData(405, "not_found", ApiProblems.NoSuchApiRouteTitle)]
    [InlineData(409, "conflict", "The request conflicts with the current data.")]
    [InlineData(429, "rate_limited", "Too many attempts. Wait a minute and try again.")]
    [InlineData(415, "validation_failed", "The request body must be JSON.")]
    [InlineData(400, "validation_failed", "The request could not be processed.")]
    [InlineData(403, "forbidden", "You are not allowed to do that.")]
    [InlineData(413, "validation_failed", "The request could not be processed.")]
    public void ForStatus_gives_each_status_its_code_and_title(int status, string code, string title)
    {
        //Act
        var problem = ApiProblems.ForStatus(status);

        //Assert
        problem.Status.Should().Be(status);
        problem.Code.Should().Be(code);
        problem.Title.Should().Be(title);
    }
}
