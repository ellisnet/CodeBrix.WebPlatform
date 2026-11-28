using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Internal;

public class DxCompilerWarningsTests
{
    private const string Escape = "\u001b";

    /// <summary>The start of a release build as the platform's dx writes it (captured from a real build), with the compiler's colour codes.</summary>
    private static readonly string DxOutput =
        "  0.00s  INFO Building project...\n"
        + $"  0.47s  INFO  {Escape}[1m{Escape}[33mwarning{Escape}[0m{Escape}[1m: function `unused_helper` is never used{Escape}[0m\n"
        + $"  {Escape}[1m{Escape}[94m--> {Escape}[0msrc/main.rs:36:4\n"
        + $"   {Escape}[1m{Escape}[94m|{Escape}[0m\n"
        + $"{Escape}[1m{Escape}[94m36{Escape}[0m {Escape}[1m{Escape}[94m|{Escape}[0m fn unused_helper() -> u32 {{\n"
        + $"   {Escape}[1m{Escape}[94m|{Escape}[0m    {Escape}[1m{Escape}[33m^^^^^^^^^^^^^{Escape}[0m\n"
        + $"   {Escape}[1m{Escape}[94m|{Escape}[0m\n"
        + $"   {Escape}[1m{Escape}[94m= {Escape}[0m{Escape}[1mnote{Escape}[0m: `#[warn(dead_code)]` (part of `#[warn(unused)]`) on by default\n"
        + "\n"
        + "\n"
        + "  0.48s  INFO Bundling app...\n"
        + "  0.50s  INFO Running wasm-bindgen...\n"
        + "  0.96s  WARN esbuild binary path not set. Copying JS without processing.\n"
        + "  0.97s  INFO Client build completed successfully!\n";

    [Fact]
    public void Read_gives_each_warning_whole_without_colour_codes_or_dx_prefix()
    {
        //Act
        var warnings = DxCompilerWarnings.Read(DxOutput);

        //Assert
        warnings.Should().HaveCount(1);
        warnings[0].Should().Be(
            "warning: function `unused_helper` is never used\n"
            + "  --> src/main.rs:36:4\n"
            + "   |\n"
            + "36 | fn unused_helper() -> u32 {\n"
            + "   |    ^^^^^^^^^^^^^\n"
            + "   |\n"
            + "   = note: `#[warn(dead_code)]` (part of `#[warn(unused)]`) on by default");
    }

    [Fact]
    public void Read_takes_no_warning_of_dx_itself()
        => DxCompilerWarnings.Read("  0.96s  WARN esbuild binary path not set. Copying JS without processing.\n  1.00s  INFO warnings_macro\n").Should().BeEmpty();

    [Fact]
    public void Read_lists_a_warning_dx_wrote_twice_once_and_keeps_the_order()
    {
        //Arrange
        var output = "  1.0s  INFO warning: unused variable: `a`\n --> src/a.rs:1:5\n"
            + "  2.0s  INFO warning[E0170]: pattern binding `B` is named the same as one of the variants\n --> src/b.rs:2:9\n"
            + "  3.0s  INFO warning: unused variable: `a`\n --> src/a.rs:1:5\n"
            + "  4.0s  INFO warning: `app` (bin \"app\") generated 2 warnings\n";

        //Act
        var warnings = DxCompilerWarnings.Read(output);

        //Assert
        warnings.Should().Equal(
            "warning: unused variable: `a`\n --> src/a.rs:1:5",
            "warning[E0170]: pattern binding `B` is named the same as one of the variants\n --> src/b.rs:2:9");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  0.00s  INFO Building project...\r\n  0.97s  INFO Client build completed successfully!\r\n")]
    public void Read_finds_nothing_in_a_build_without_warnings(string output)
        => DxCompilerWarnings.Read(output).Should().BeEmpty();
}
