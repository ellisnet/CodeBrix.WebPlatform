using System.IO;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests;

public class RepositoryLayoutTests
{
    [Fact]
    public void Find_walks_up_to_the_folder_that_holds_the_solution_and_the_allowlist()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("CodeBrix.WebPlatform.slnx", "<Solution />");
        folder.Write("tools/licensing/license-allowlist.json", "{}");
        var deep = Directory.CreateDirectory(Path.Combine(folder.Path, "tests", "x", "bin", "Debug")).FullName;

        //Act
        var fromInside = RepositoryLayout.Find(deep);
        var fromRoot = RepositoryLayout.Find(folder.Path);

        //Assert
        fromInside.Root.Should().Be(folder.Path);
        fromRoot.Root.Should().Be(folder.Path);
    }

    [Fact]
    public void Find_returns_null_when_there_is_no_repository_root()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("tools/licensing/license-allowlist.json", "{}");

        //Assert
        RepositoryLayout.Find(folder.Path).Should().BeNull("the solution file is missing");
        RepositoryLayout.Find(" ").Should().BeNull();
        RepositoryLayout.IsRoot(folder.Path).Should().BeFalse();
    }

    [Fact]
    public void the_paths_hang_off_the_root_and_nothing_is_under_samples()
    {
        //Arrange
        var layout = new RepositoryLayout(Path.Combine(Path.GetTempPath(), "repo"));
        var rust = Path.Combine(layout.Root, "src", "CodeBrix.WebPlatform.FrontendBuilder", "Rust");

        //Assert
        layout.PlatformVendor.Should().Be(Path.Combine(rust, "vendor"));
        layout.PlatformCrateSetLock.Should().Be(Path.Combine(rust, "platform-crates", "Cargo.lock"));
        layout.SharedCrateLock.Should().Be(Path.Combine(rust, "codebrix-webplatform", "Cargo.lock"));
        layout.PlatformNoticesFile.Should().Be(Path.Combine(rust, "notices", "THIRD-PARTY-NOTICES-PlatformCrates.txt"));
        layout.ToolingVendor.Should().Be(Path.Combine(layout.Root, "tools", "dx-native-tools", "sources", "vendor"));
        layout.DioxusCli.Should().Be(Path.Combine(layout.Root, "tools", "dx-native-tools", "sources", "dioxus-cli"));
        layout.ToolVendoring.Should().Be(Path.Combine(layout.Root, "tools", "dx-native-tools", "vendoring"));
        layout.AllowlistFile.Should().Be(Path.Combine(layout.Root, "tools", "licensing", "license-allowlist.json"));
        layout.ToolSourcesRelative.Should().Be("tools/dx-native-tools/sources");
        foreach (var path in new[] { layout.PlatformVendor, layout.ToolSources, layout.ToolVendoring, layout.AllowlistFile, layout.PlatformNoticesFile })
        {
            path.Should().NotStartWith(layout.Samples);
        }
    }
}
