using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Prerequisites;

public class PrerequisiteReportTests
{
    private static PrerequisiteReport Create(PrerequisiteStatus tailwind)
        => new PrerequisiteReport(
            PrerequisiteRules.Ordered(new[]
            {
                new PrerequisiteItem(PrerequisiteKind.HostTools, PrerequisiteStatus.Ok, "tools"),
                new PrerequisiteItem(PrerequisiteKind.Tailwind, tailwind, "tailwind"),
                new PrerequisiteItem(PrerequisiteKind.WasmOpt, PrerequisiteStatus.Note, "wasm-opt"),
                new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Ok, "rust"),
            }),
            "1.100.0",
            "4.3.5",
            WasmOptSource.Installed,
            null);

    [Fact]
    public void a_note_does_not_make_the_machine_unready()
    {
        //Act
        var report = Create(PrerequisiteStatus.Ok);

        //Assert
        report.IsReady.Should().BeTrue();
        report.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Problems_lists_the_missing_items()
    {
        //Act
        var report = Create(PrerequisiteStatus.Missing);

        //Assert
        report.IsReady.Should().BeFalse();
        report.Problems.Should().Equal("tailwind");
    }

    [Fact]
    public void the_items_are_in_the_order_of_their_kinds_and_can_be_looked_up()
    {
        //Act
        var report = Create(PrerequisiteStatus.Ok);

        //Assert
        report.Items.Should().HaveCount(4);
        report.Items[0].Kind.Should().Be(PrerequisiteKind.Rust);
        report.Items[3].Kind.Should().Be(PrerequisiteKind.HostTools);
        report[PrerequisiteKind.WasmOpt].Message.Should().Be("wasm-opt");
    }

    [Theory]
    [InlineData(PrerequisiteStatus.Ok, "OK       done")]
    [InlineData(PrerequisiteStatus.Note, "NOTE     done")]
    [InlineData(PrerequisiteStatus.Missing, "MISSING  done")]
    public void PrerequisiteItem_ToString_starts_with_the_status(PrerequisiteStatus status, string expected)
        => new PrerequisiteItem(PrerequisiteKind.Rust, status, "done").ToString().Should().Be(expected);
}
