using System;
using System.IO;
using SilverAssertions;
using WorkSite.BuildFrontend.Tests.Helpers;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class WorkSiteLayoutTests
{
    [Fact]
    public void Find_walks_up_to_the_folder_that_holds_the_solution()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("samples/WorkSite/WorkSite.slnx", "<Solution />");
        var deep = Directory.CreateDirectory(Path.Combine(folder.Path, "samples", "WorkSite", "build", "x", "y")).FullName;

        //Act
        var fromInside = WorkSiteLayout.Find(deep);
        var fromRepositoryRoot = WorkSiteLayout.Find(folder.Path);

        //Assert
        fromInside.Root.Should().Be(Path.Combine(folder.Path, "samples", "WorkSite"));
        fromRepositoryRoot.Root.Should().Be(fromInside.Root);
        fromInside.RepositoryRoot.Should().Be(folder.Path);
        fromInside.Frontend.Should().Be(Path.Combine(fromInside.Root, "frontend"));
        fromInside.NoticesFile.Should().Be(Path.Combine(fromInside.Root, "THIRD-PARTY-NOTICES.txt"));
    }

    [Fact]
    public void Find_returns_null_when_there_is_no_WorkSite_folder()
    {
        //Arrange
        using var folder = new TempFolder();

        //Assert
        WorkSiteLayout.Find(folder.Path).Should().BeNull();
        WorkSiteLayout.Find(" ").Should().BeNull();
        WorkSiteLayout.IsRoot(folder.Path).Should().BeFalse();
    }

    [Fact]
    public void the_temporary_package_feed_is_not_inside_the_sample()
    {
        //Arrange
        var layout = WorkSiteLayout.Find(AppContext.BaseDirectory);

        //Assert
        File.Exists(Path.Combine(layout.Root, "nuget.config")).Should().BeFalse("the one temporary feed is at the repository root");
        Directory.Exists(Path.Combine(layout.Root, "temp-packages")).Should().BeFalse("the one temporary feed is at the repository root");
        File.Exists(Path.Combine(layout.RepositoryRoot, "nuget.config")).Should().BeTrue();
    }

    [Fact]
    public void the_sample_holds_no_vendored_crates_and_no_tool_sources()
    {
        //Arrange
        var layout = WorkSiteLayout.Find(AppContext.BaseDirectory);

        //Assert
        foreach (var gone in new[] { "vendor", "vendoring", "tools" })
        {
            Directory.Exists(Path.Combine(layout.Root, gone)).Should().BeFalse($"{gone} belongs to the platform now (the build program is in build/)");
        }
    }
}
