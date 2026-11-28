using System.Collections.Generic;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Internal;

public class SharedChunkSettingsTests
{
    [Fact]
    public void Apply_sets_both_variables_whatever_the_environment_held()
    {
        //Arrange
        var options = new PlatformFrontendOptions { FrontendDirectory = "frontend", SharedChunkThreshold = 4096 };
        var plan = ApplicationPlan.Create(options).Single();
        var environment = new Dictionary<string, string>
        {
            [SharedChunkSettings.ThresholdVariable] = "1",
            [SharedChunkSettings.LimitVariable] = "not a number",
        };

        //Act
        SharedChunkSettings.Apply(environment, plan);

        //Assert
        environment["DX_WASM_SPLIT_CHUNK_THRESHOLD"].Should().Be("4096");
        environment["DX_WASM_SPLIT_CHUNK_LIMIT"].Should().Be("16");
    }

    [Theory]
    [InlineData("   12.31s  INFO Bundle splitting: 3 shared chunks (threshold 16384 bytes, limit 16)", 3)]
    [InlineData("INFO Bundle splitting: 1 shared chunk (threshold 16384 bytes, limit 1)", 1)]
    [InlineData("Bundle splitting: 1 shared chunk (no threshold: one chunk for all shared code)", 1)]
    public void ReportedChunks_reads_the_count_from_dxs_line(string line, int expected)
    {
        //Act
        var count = SharedChunkSettings.ReportedChunks("INFO Compiling\n" + line + "\nINFO Bundled");

        //Assert
        count.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("INFO Splitting bundle\nINFO Bundled")]
    public void ReportedChunks_is_null_when_dx_printed_no_such_line(string output)
    {
        //Act
        var count = SharedChunkSettings.ReportedChunks(output);

        //Assert
        count.Should().BeNull();
    }

    [Fact]
    public void MismatchMessage_says_that_dx_does_not_match_this_library()
    {
        //Act
        var message = SharedChunkSettings.MismatchMessage("the frontend application 'sales'");

        //Assert
        message.Should().StartWith("dx built the frontend application 'sales' with bundle splitting but did not report its shared chunks");
        message.Should().Contain("the dx program does not match this library");
        message.Should().Contain("DX_WASM_SPLIT_CHUNK_THRESHOLD");
    }
}
