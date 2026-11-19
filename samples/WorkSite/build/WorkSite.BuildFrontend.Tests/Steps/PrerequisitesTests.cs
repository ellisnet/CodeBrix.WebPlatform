using System;
using System.IO;
using SilverAssertions;
using WorkSite.BuildFrontend.Steps;
using WorkSite.BuildFrontend.Tests.Helpers;
using Xunit;

namespace WorkSite.BuildFrontend.Tests.Steps;

public class PrerequisitesTests
{
    [Fact]
    public void ExpectedTailwindVersion_reads_the_pin_file()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("tailwindcss/version.txt", "4.3.5\n");
        var prerequisites = new Prerequisites(new ToolRunner(null), new BuilderPaths(new WorkSiteLayout(folder.Path)));

        //Assert
        prerequisites.ExpectedTailwindVersion().Should().Be("4.3.5");
    }

    [Fact]
    public void ExpectedTailwindVersion_fails_clearly_without_a_pin_file()
    {
        //Arrange
        using var folder = new TempFolder();
        var prerequisites = new Prerequisites(new ToolRunner(null), new BuilderPaths(new WorkSiteLayout(folder.Path)));
        Action act = () => prerequisites.ExpectedTailwindVersion();

        //Assert
        act.Should().Throw<BuilderException>().WithMessage("*version.txt*");
    }

    [Fact]
    public void the_repository_pins_a_stable_tailwind_version()
    {
        //Arrange
        var layout = WorkSiteLayout.Find(AppContext.BaseDirectory);
        var prerequisites = new Prerequisites(new ToolRunner(null), new BuilderPaths(layout));

        //Act
        var version = prerequisites.ExpectedTailwindVersion();

        //Assert
        Version.TryParse(version, out var parsed).Should().BeTrue();
        parsed.Major.Should().Be(4);
        File.Exists(Path.Combine(layout.Root, "tailwindcss", "UPGRADE-README.txt")).Should().BeTrue();
    }
}
