using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.LibraryChecker.Checks;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests.Checks;

public class NoNodeCheckTests
{
    [Fact]
    public void Scan_passes_for_a_tree_of_rust_and_dotnet_sources()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("frontend/src/main.rs", "fn main() {}");
        folder.Write("frontend/tailwind.css", "@import \"tailwindcss\";");
        folder.Write("backend/Api/Program.cs", "class Program {}");

        //Act
        var result = NoNodeCheck.Scan(folder.Path, null);

        //Assert
        result.Passed.Should().BeTrue(result.ToText());
        result.Notes.Single().Should().Be("3 files scanned");
    }

    [Theory]
    [InlineData("frontend/package.json")]
    [InlineData("package-lock.json")]
    [InlineData("frontend/yarn.lock")]
    [InlineData("pnpm-lock.yaml")]
    [InlineData("frontend/tailwind.config.js")]
    [InlineData("frontend/src/helper.js")]
    [InlineData("frontend/src/app.ts")]
    [InlineData("backend/Api/Scripts/site.mjs")]
    [InlineData(".npmrc")]
    public void Scan_fails_for_node_tooling_files_and_script_sources(string relativePath)
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write(relativePath, "x");

        //Act
        var result = NoNodeCheck.Scan(folder.Path, null);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Single().Should().StartWith(relativePath);
    }

    [Fact]
    public void Scan_fails_for_a_node_modules_folder_without_walking_into_it()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("frontend/node_modules/left-pad/index.js", "x");

        //Act
        var result = NoNodeCheck.Scan(folder.Path, null);

        //Assert
        result.Problems.Should().ContainSingle().Which.Should().Be("frontend/node_modules/ is a Node.js folder");
    }

    [Fact]
    public void Scan_leaves_out_upstream_snapshots_and_build_output()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("vendor/some-crate/package.json", "{}");
        folder.Write("tools/dioxus-cli/assets/web/loader.js", "x");
        folder.Write("frontend/target/dx/public/assets/app.js", "x");
        folder.Write("backend/Api/wwwroot/assets/app.js", "x");
        folder.Write("backend/Api/bin/Debug/thing.js", "x");
        var skipped = new[] { Path.Combine(folder.Path, "vendor"), Path.Combine(folder.Path, "tools", "dioxus-cli") };

        //Act
        var result = NoNodeCheck.Scan(folder.Path, skipped);

        //Assert
        result.Passed.Should().BeTrue(result.ToText());
    }

    [Fact]
    public void Run_checks_the_repository_but_not_the_samples_the_vendored_trees_or_the_programs()
    {
        //Arrange
        using var folder = new TempFolder();
        var layout = new RepositoryLayout(folder.Path);
        folder.Write("samples/SomeApp/frontend/package.json", "{}");
        folder.Write("src/CodeBrix.WebPlatform.FrontendBuilder/Rust/vendor/a-crate/package.json", "{}");
        folder.Write("tools/dx-native-tools/sources/dioxus-cli/assets/web/loader.js", "x");
        folder.Write("tools/dx-native-tools/output/functional-gate/site/app.js", "x");
        folder.Write("src/Library/helper.js", "x");

        //Act
        var result = NoNodeCheck.Run(layout);

        //Assert
        result.Problems.Should().ContainSingle().Which.Should().StartWith("src/Library/helper.js");
    }
}
