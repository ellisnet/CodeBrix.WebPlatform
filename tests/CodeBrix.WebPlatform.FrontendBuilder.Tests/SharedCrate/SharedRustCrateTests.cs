using System;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.SharedCrate;

public class SharedRustCrateTests
{
    [Fact]
    public void Files_holds_the_manifest_and_the_source()
    {
        //Act
        var paths = SharedRustCrate.Files.Select(f => f.RelativePath).ToList();

        //Assert
        paths.Should().Contain("Cargo.toml");
        paths.Should().Contain("src/lib.rs");
        paths.Should().Contain("src/api/client.rs");
        paths.Should().Contain("src/api/error.rs");
        paths.Should().Contain("src/dates.rs");
        paths.Should().Contain("src/query.rs");
        paths.Should().Contain("src/app_runtime.rs");
        paths.Should().Contain("src/signalr/mod.rs");
        paths.Should().Contain("src/signalr/connection.rs");
        paths.Should().Contain("src/signalr/testing.rs");
        paths.Should().Contain("src/signalr/web.rs");
        paths.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Files_leaves_out_cargo_output_and_the_repository_only_cargo_config()
    {
        //Act
        var paths = SharedRustCrate.Files.Select(f => f.RelativePath).ToList();

        //Assert
        paths.Should().NotContain("Cargo.lock");
        paths.Where(p => p.StartsWith("target/", StringComparison.Ordinal)).Should().BeEmpty();
        paths.Where(p => p.StartsWith(".cargo/", StringComparison.Ordinal)).Should().BeEmpty();
        paths.Where(p => p.Contains('\\')).Should().BeEmpty();
    }

    [Fact]
    public void Files_manifest_names_the_crate()
    {
        //Act
        var manifest = Encoding.UTF8.GetString(SharedRustCrate.Files.Single(f => f.RelativePath == "Cargo.toml").Content);

        //Assert
        manifest.Should().Contain($"name = \"{SharedRustCrate.CrateName}\"");
        SharedRustCrate.RustName.Should().Be(SharedRustCrate.CrateName.Replace('-', '_'));
    }

    [Fact]
    public void ExtractTo_writes_every_file_into_a_new_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var target = Path.Combine(folder.Path, "platform", "codebrix-webplatform");

        //Act
        var changed = SharedRustCrate.ExtractTo(target);

        //Assert
        changed.Should().Equal(SharedRustCrate.Files.Select(f => f.RelativePath));
        foreach (var file in SharedRustCrate.Files)
        {
            File.ReadAllBytes(Path.Combine(target, file.RelativePath.Replace('/', Path.DirectorySeparatorChar))).Should().Equal(file.Content);
        }
    }

    [Fact]
    public void ExtractTo_touches_nothing_when_the_folder_is_up_to_date()
    {
        //Arrange
        using var folder = new TempFolder();
        SharedRustCrate.ExtractTo(folder.Path);
        var library = Path.Combine(folder.Path, "src", "lib.rs");
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(library, stamp);

        //Act
        var changed = SharedRustCrate.ExtractTo(folder.Path);

        //Assert
        changed.Should().BeEmpty();
        File.GetLastWriteTimeUtc(library).Should().Be(stamp);
    }

    [Fact]
    public void ExtractTo_restores_a_changed_file_and_removes_a_stray_one()
    {
        //Arrange
        using var folder = new TempFolder();
        SharedRustCrate.ExtractTo(folder.Path);
        folder.Write("src/lib.rs", "// edited by hand");
        folder.Write("src/stray.rs", "// not part of the crate");

        //Act
        var changed = SharedRustCrate.ExtractTo(folder.Path);

        //Assert
        changed.Should().Equal("src/lib.rs", "src/stray.rs");
        File.Exists(Path.Combine(folder.Path, "src", "stray.rs")).Should().BeFalse();
        File.ReadAllBytes(Path.Combine(folder.Path, "src", "lib.rs")).Should().Equal(SharedRustCrate.Files.Single(f => f.RelativePath == "src/lib.rs").Content);
    }

    [Fact]
    public void ExtractTo_keeps_cargos_own_output()
    {
        //Arrange
        using var folder = new TempFolder();
        SharedRustCrate.ExtractTo(folder.Path);
        folder.Write("Cargo.lock", "# lock");
        folder.Write("target/debug/build.log", "log");

        //Act
        var changed = SharedRustCrate.ExtractTo(folder.Path);

        //Assert
        changed.Should().BeEmpty();
        File.Exists(Path.Combine(folder.Path, "Cargo.lock")).Should().BeTrue();
        File.Exists(Path.Combine(folder.Path, "target", "debug", "build.log")).Should().BeTrue();
    }

    [Fact]
    public void ExtractTo_refuses_a_folder_that_holds_something_else()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("notes.txt", "important");

        //Act
        Action act = () => SharedRustCrate.ExtractTo(folder.Path);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*not empty*Nothing was written*");
        File.ReadAllText(Path.Combine(folder.Path, "notes.txt")).Should().Be("important");
        Directory.EnumerateFileSystemEntries(folder.Path).Count().Should().Be(1);
    }

    [Fact]
    public void ExtractTo_refuses_another_crates_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("Cargo.toml", "[package]\nname = \"someone-elses-crate\"\n");

        //Act
        Action act = () => SharedRustCrate.ExtractTo(folder.Path);

        //Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ExtractTo_refuses_a_blank_folder_name(string directory)
    {
        //Act
        Action act = () => SharedRustCrate.ExtractTo(directory);

        //Assert
        act.Should().Throw<ArgumentException>();
    }
}
