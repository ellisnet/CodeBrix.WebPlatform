using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.LibraryChecker.Checks;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests.Checks;

public class NoLibgit2SourceCheckTests
{
    private static void WriteStandIn(TempFolder folder)
    {
        folder.Write("tools/sources/codebrix-libgit2-sys/Cargo.toml", "[package]\nname = \"libgit2-sys\"\n");
        folder.Write("tools/sources/codebrix-libgit2-sys/build.rs", "fn main() {}");
        folder.Write("tools/sources/codebrix-libgit2-sys/lib.rs", "extern \"C\" {}");
        folder.Write("tools/sources/codebrix-libgit2-sys/LICENSE-MIT", "MIT");
        folder.Write("tools/sources/vendor/git2-0.20.4/Cargo.toml", "[package]");
    }

    private static CheckResult Scan(TempFolder folder)
        => NoLibgit2SourceCheck.Scan(folder.Path, Path.Combine(folder.Path, "tools/sources/codebrix-libgit2-sys"), new[] { Path.Combine(folder.Path, "tools/sources/vendor") });

    [Fact]
    public void Scan_passes_for_a_declarations_only_stand_in_and_a_vendor_folder_without_libgit2_sys()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write("tools/sources/vendor/ring-0.17.14/include/ring-core/base.h", "x");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeTrue(result.ToText());
        result.Notes.Single().Should().Be("stand-in: 4 files; 2 vendored crate folders checked; 6 files scanned for libgit2's source");
    }

    [Fact]
    public void Scan_fails_when_the_stand_in_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("tools/sources/vendor/git2-0.20.4/Cargo.toml", "[package]");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Single().Should().Contain("the libgit2-sys stand-in is missing");
    }

    [Theory]
    [InlineData("tools/sources/codebrix-libgit2-sys/shim.c")]
    [InlineData("tools/sources/codebrix-libgit2-sys/git2-extra.h")]
    public void Scan_fails_when_the_stand_in_holds_c_code(string relativePath)
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write(relativePath, "int x;");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Single().Should().StartWith(relativePath);
    }

    [Fact]
    public void Scan_fails_when_the_stand_in_has_a_sub_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write("tools/sources/codebrix-libgit2-sys/libgit2/README.md", "x");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Should().Contain("tools/sources/codebrix-libgit2-sys/libgit2/ is a folder; the stand-in holds its few files only");
    }

    [Theory]
    [InlineData("libgit2-sys-0.18.3+1.9.2")]
    [InlineData("libgit2-sys")]
    public void Scan_fails_when_the_crates_io_libgit2_sys_is_vendored(string crateFolder)
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write($"tools/sources/vendor/{crateFolder}/Cargo.toml", "[package]");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Single().Should().StartWith($"tools/sources/vendor/{crateFolder} is the crates.io crate libgit2-sys");
    }

    [Fact]
    public void Scan_fails_for_libgit2s_header_and_header_folder_anywhere_in_the_tree()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write("samples/copied/libgit2/include/git2.h", "x");
        folder.Write("samples/copied/libgit2/include/git2/repository.h", "x");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Should().Equal(
            "samples/copied/libgit2/include/git2.h is libgit2's public header",
            "samples/copied/libgit2/include/git2/ is libgit2's header folder");
    }

    [Fact]
    public void Scan_leaves_out_git_and_build_output_folders()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteStandIn(folder);
        folder.Write(".git/objects/git2.h", "x");
        folder.Write("tools/sources/codebrix-libgit2-sys/target/release/build/out.c", "x");
        folder.Write("libs/Checker/obj/include/git2/x.h", "x");

        //Act
        var result = Scan(folder);

        //Assert
        result.Passed.Should().BeTrue(result.ToText());
    }
}
