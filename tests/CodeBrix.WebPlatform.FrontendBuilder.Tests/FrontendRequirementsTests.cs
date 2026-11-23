using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

public class FrontendRequirementsTests
{
    [Theory]
    [InlineData("≈ tailwindcss v4.3.5\n\nUsage:\n  tailwindcss ...", "4.3.5")]
    [InlineData("tailwindcss v4.10.12", "4.10.12")]
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
