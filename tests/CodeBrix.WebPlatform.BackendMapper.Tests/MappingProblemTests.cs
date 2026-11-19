using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests;

public class MappingProblemTests
{
    [Fact]
    public void ToString_joins_the_location_and_the_message()
        => new MappingProblem("ClientsController.Update", "the success answer is not declared.").ToString()
            .Should().Be("ClientsController.Update: the success answer is not declared.");

    [Fact]
    public void a_single_problem_is_counted_in_the_singular()
    {
        //Act
        var exception = new BackendMapException(new[] { new MappingProblem("A", "b") });

        //Assert
        exception.Message.Should().Be("The backend cannot be mapped: 1 problem." + Environment.NewLine + "  - A: b");
        exception.Problems.Count.Should().Be(1);
    }

    [Fact]
    public void an_exception_about_the_whole_backend_carries_no_problems()
    {
        //Act
        var exception = new BackendMapException("The assembly could not be read.");

        //Assert
        exception.Problems.Should().BeEmpty();
        exception.Message.Should().Be("The assembly could not be read.");
    }
}
