using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

public class PlatformCratesTests
{
    [Fact]
    public void the_package_puts_the_crates_archive_beside_the_program_that_references_it()
    {
        //Arrange
        var archive = PlatformCrates.GetArchivePath();

        //Act
        using var zip = ZipFile.OpenRead(archive);
        var crates = zip.Entries.Select(e => e.FullName.Replace('\\', '/').Split('/')[0]).Distinct(StringComparer.Ordinal).ToList();

        //Assert
        archive.Should().Be(Path.Combine(AppContext.BaseDirectory, "frontend-crates", "platform-crates.zip"));
        crates.Should().Contain("dioxus-0.7.10");
        crates.Should().Contain(c => c.StartsWith("reqwest-", StringComparison.Ordinal));
        zip.Entries.Should().Contain(e => e.FullName.Replace('\\', '/') == "dioxus-0.7.10/.cargo-checksum.json");
        crates.Should().NotContain("dioxus-cli-0.7.10", "the tools' source is not among the platform's crates");
    }

    [Fact]
    public void the_archive_holds_exactly_the_crates_the_platforms_lock_file_names()
    {
        //Arrange
        var locked = PlatformCrates.LockFileText.Split('\n')
            .Count(l => l.StartsWith("source = \"registry+", StringComparison.Ordinal));

        //Act
        using var zip = ZipFile.OpenRead(PlatformCrates.GetArchivePath());
        var crates = zip.Entries.Select(e => e.FullName.Replace('\\', '/').Split('/')[0]).Distinct(StringComparer.Ordinal).Count();

        //Assert
        crates.Should().Be(locked);
    }

    [Fact]
    public void ReadNotices_gives_the_generated_notices_with_the_shared_crates_own_third_party_code()
    {
        //Act
        var notices = PlatformCrates.ReadNotices();

        //Assert
        notices.Should().Contain("signalr-client 0.3.2");
        notices.Should().Contain("PART 1 - the platform's crates compiled into a platform application");
        notices.Should().Contain("dioxus 0.7.10");
        File.Exists(PlatformCrates.GetNoticesPath()).Should().BeTrue();
    }

    [Fact]
    public void ReadNotices_says_what_to_do_when_the_file_is_not_beside_the_build_program()
    {
        //Arrange
        using var folder = new TempFolder();
        Action act = () => PlatformCrates.ReadNotices(folder.Path);

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*THIRD-PARTY-NOTICES-PlatformCrates.txt' does not exist*");
    }

    [Fact]
    public void LockFileText_is_the_platforms_lock_file()
    {
        //Act
        var text = PlatformCrates.LockFileText;

        //Assert
        text.Should().Contain("name = \"dioxus\"\nversion = \"0.7.10\"");
        text.Should().Contain("name = \"codebrix-webplatform\"");
    }

    [Fact]
    public void ExtractTo_writes_the_folders_gitignore_also_when_the_crates_are_unchanged()
    {
        //Arrange
        using var folder = new TempFolder();
        var archive = Frontends.WriteCratesArchive(folder);
        var vendor = folder.Combine("frontend/platform/vendor");
        var ignoreFile = Path.Combine(vendor, BuildOutputFolder.IgnoreFileName);

        //Act
        var first = PlatformCrates.ExtractTo(archive, vendor);
        var written = File.ReadAllText(ignoreFile);
        File.Delete(ignoreFile);
        var second = PlatformCrates.ExtractTo(archive, vendor);

        //Assert
        first.Should().BeTrue();
        second.Should().BeFalse("the stamp says the crates are the same");
        written.Should().Be(BuildOutputFolder.IgnoreFileText);
        File.ReadAllText(ignoreFile).Should().Be(BuildOutputFolder.IgnoreFileText);
        PlatformCrates.ListCrates(vendor).Should().Equal("fake-crate-1.0.0");
    }

    [Fact]
    public void ExtractTo_counts_a_folder_that_holds_only_its_gitignore_as_its_own()
    {
        //Arrange
        using var folder = new TempFolder();
        var archive = Frontends.WriteCratesArchive(folder);
        var vendor = folder.Combine("vendor");
        folder.Write("vendor/" + BuildOutputFolder.IgnoreFileName, BuildOutputFolder.IgnoreFileText);

        //Act
        var written = PlatformCrates.ExtractTo(archive, vendor);

        //Assert
        written.Should().BeTrue();
        File.Exists(Path.Combine(vendor, PlatformCrates.StampFileName)).Should().BeTrue();
    }

    [Fact]
    public void ListCrates_leaves_out_the_stamp_and_other_dot_entries()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("vendor/b-1.0.0/Cargo.toml", "x");
        folder.Write("vendor/a-1.0.0/Cargo.toml", "x");
        folder.Write("vendor/.staging/Cargo.toml", "x");
        folder.Write("vendor/" + PlatformCrates.StampFileName, "hash");

        //Act
        var crates = PlatformCrates.ListCrates(folder.Combine("vendor"));

        //Assert
        crates.Should().Equal("a-1.0.0", "b-1.0.0");
    }
}
