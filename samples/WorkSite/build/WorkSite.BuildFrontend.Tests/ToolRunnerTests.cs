using System.IO;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class ToolRunnerTests
{
    [Fact]
    public async Task RunAsync_captures_output_and_the_exit_code()
    {
        //Arrange
        var output = new StringWriter();
        var runner = new ToolRunner(output);

        //Act
        var result = await runner.RunAsync("dotnet", new[] { "--version" }, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        result.Succeeded.Should().BeTrue();
        result.Output.Trim().Should().Contain(".");
        output.ToString().Should().Contain(result.Output.Trim());
    }

    [Fact]
    public async Task RunAsync_can_run_quietly_and_reports_a_failing_exit_code()
    {
        //Arrange
        var output = new StringWriter();
        var runner = new ToolRunner(output);

        //Act
        var result = await runner.RunAsync("dotnet", new[] { "no-such-dotnet-command-worksite" }, echo: false, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        result.Succeeded.Should().BeFalse();
        result.NotFound.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_reports_a_program_that_does_not_exist()
    {
        //Arrange
        var runner = new ToolRunner(null);

        //Act
        var result = await runner.RunAsync("no-such-program-worksite-tests", new[] { "x" }, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        result.NotFound.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
    }

    [Fact]
    public void Tail_keeps_the_last_non_blank_lines_and_shortens_very_long_ones()
    {
        //Act
        var tail = ToolRunner.Tail("one\n\ntwo\nthree\n" + new string('x', 900) + "\n", 2);

        //Assert
        tail.Should().NotContain("one").And.NotContain("two").And.Contain("three").And.Contain(" ...");
        ToolRunner.Tail(null, 5).Should().BeEmpty();
    }
}
