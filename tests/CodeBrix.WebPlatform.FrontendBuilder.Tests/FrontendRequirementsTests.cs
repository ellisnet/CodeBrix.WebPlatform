using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

public class FrontendRequirementsTests
{
    [Theory]
    [InlineData("≈ tailwindcss v4.3.5\n\nUsage:\n  tailwindcss ...", "4.3.5")]
    [InlineData("tailwindcss v4.10.12", "4.10.12")]
    // As the program prints it on Windows, coloured even when its output is not a terminal.
    [InlineData("\u001b[3m\u001b[1m\u001b[34m≈\u001b[39m\u001b[22m\u001b[23m tailwindcss \u001b[34mv4.3.5\u001b[39m\n\nUsage:", "4.3.5")]
    [InlineData("some other program 1.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseTailwindVersion_reads_the_version_from_the_banner(string output, string expected)
        => FrontendRequirements.ParseTailwindVersion(output).Should().Be(expected);

    [Fact]
    public void the_minimum_versions_are_versions()
    {
        //Assert
        Version.TryParse(FrontendRequirements.MinimumRustVersion, out var rust).Should().BeTrue();
        rust.Should().BeGreaterThanOrEqualTo(new Version(1, 100, 0));
        FrontendRequirements.WebAssemblyTarget.Should().Be("wasm32-unknown-unknown");
    }
}
