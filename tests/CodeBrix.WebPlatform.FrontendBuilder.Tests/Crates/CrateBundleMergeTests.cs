using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

public class CrateBundleMergeTests
{
    private static readonly Bundles.Crate Syn2 = new Bundles.Crate("syn", "2.0.1");
    private static readonly Bundles.Crate Syn3 = new Bundles.Crate("syn", "3.0.1");
    private static readonly Bundles.Crate WebSys = new Bundles.Crate("web-sys", "0.3.1");

    private static string Platform(TempFolder folder) => Bundles.WritePlatformArchive(folder, Syn2, Syn3, WebSys);

    [Fact]
    public void Check_without_bundles_stamps_the_folder_with_the_platform_archives_hash()
    {
        //Arrange
        using var folder = new TempFolder();
        var archive = Platform(folder);

        //Act
        var merge = CrateBundleMerge.Check(archive, Array.Empty<CrateBundle>());

        //Assert
        merge.Outcomes.Should().BeEmpty();
        merge.Stamp.Should().Be(merge.PlatformHash);
        merge.PlatformHash.Should().HaveLength(64);
    }

    [Fact]
    public void Check_says_what_a_bundle_adds_and_what_it_shares_with_the_platform()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("wgpu", "30.0.2"), WebSys, new Bundles.Crate("arrayvec", "0.7.8"));

        //Act
        var merge = CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        var outcome = merge.Outcomes.Single();
        outcome.Name.Should().Be("graphics");
        outcome.Origin.Should().Be("My.Graphics 1.0");
        outcome.AddedCrates.Should().Equal("arrayvec 0.7.8", "wgpu 30.0.2");
        outcome.SharedCrates.Should().Equal("web-sys 0.3.1");
        merge.Stamp.Should().StartWith("bundles-");
    }

    [Fact]
    public void Check_lets_a_bundle_carry_either_of_two_versions_the_platform_itself_has()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("b", "P", "1", Syn2, Syn3);

        //Act
        var merge = CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        merge.Outcomes.Single().SharedCrates.Should().Equal("syn 2.0.1", "syn 3.0.1");
        merge.Outcomes.Single().AddedCrates.Should().BeEmpty();
    }

    [Fact]
    public void Check_lets_several_bundles_carry_the_same_crate()
    {
        //Arrange
        using var folder = new TempFolder();
        var wgpu = new Bundles.Crate("wgpu", "30.0.2");
        var first = Bundles.Create("graphics", "My.Graphics", "1.0", wgpu);
        var second = Bundles.Create("animation", "My.Animation", "1.0", wgpu, new Bundles.Crate("lottie", "1.0.0"));

        //Act
        var merge = CrateBundleMerge.Check(Platform(folder), new[] { first, second });

        //Assert
        merge.Outcomes[0].AddedCrates.Should().Equal("wgpu 30.0.2");
        merge.Outcomes[1].SharedCrates.Should().Equal("wgpu 30.0.2");
        merge.Outcomes[1].AddedCrates.Should().Equal("lottie 1.0.0");
    }

    [Fact]
    public void Check_lets_one_bundle_bring_two_versions_of_a_crate_nobody_else_has()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("b", "P", "1", new Bundles.Crate("hashy", "1.0.0"), new Bundles.Crate("hashy", "2.0.0"));

        //Act
        var merge = CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        merge.Outcomes.Single().AddedCrates.Should().Equal("hashy 1.0.0", "hashy 2.0.0");
    }

    [Fact]
    public void Check_refuses_the_same_crate_with_different_content_and_names_both_sides()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("web-sys", "0.3.1", "changed"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        act.Should().Throw<FrontendBuildException>()
            .WithMessage("*Nothing was written*web-sys 0.3.1: the crate bundle 'graphics' of My.Graphics 1.0 and the platform's crates*different content*");
    }

    [Fact]
    public void Check_refuses_a_version_the_platform_carries_differently()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("web-sys", "0.3.2"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        act.Should().Throw<FrontendBuildException>()
            .WithMessage("*web-sys: the crate bundle 'graphics' of My.Graphics 1.0 carries version 0.3.2, but the platform's crates*carries version 0.3.1*");
    }

    [Fact]
    public void Check_refuses_two_bundles_that_bring_different_versions_of_a_crate()
    {
        //Arrange
        using var folder = new TempFolder();
        var first = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("wgpu", "30.0.2"));
        var second = Bundles.Create("charts", "Their.Charts", "4.5", new Bundles.Crate("wgpu", "29.0.4"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { first, second });

        //Assert
        act.Should().Throw<FrontendBuildException>()
            .WithMessage("*wgpu: the crate bundle 'charts' of Their.Charts 4.5 carries version 29.0.4, but the crate bundle 'graphics' of My.Graphics 1.0 carries version 30.0.2*");
    }

    [Fact]
    public void Check_refuses_two_bundles_with_the_same_crate_and_different_content()
    {
        //Arrange
        using var folder = new TempFolder();
        var first = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("wgpu", "30.0.2"));
        var second = Bundles.Create("charts", "Their.Charts", "4.5", new Bundles.Crate("wgpu", "30.0.2", "patched"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { first, second });

        //Assert
        act.Should().Throw<FrontendBuildException>()
            .WithMessage("*wgpu 30.0.2: the crate bundle 'charts' of Their.Charts 4.5 and the crate bundle 'graphics' of My.Graphics 1.0 both carry it, with different content*");
    }

    [Fact]
    public void Check_lists_every_crate_that_breaks_the_rule_in_one_message()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("b", "P", "1", new Bundles.Crate("web-sys", "0.3.2"), new Bundles.Crate("syn", "2.0.1", "changed"), new Bundles.Crate("fine", "1.0.0"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        var message = act.Should().Throw<FrontendBuildException>().Which.Message;
        message.Should().Contain("  - web-sys: ");
        message.Should().Contain("  - syn 2.0.1: ");
        message.Should().NotContain("fine");
    }

    [Fact]
    public void Check_refuses_an_archive_that_is_not_a_zip_file()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = new CrateBundle("broken", "P", "1", () => new MemoryStream(new byte[] { 1, 2, 3, 4 }));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*the crate bundle 'broken' of P 1*not a readable zip file*");
    }

    [Fact]
    public void Check_refuses_an_archive_without_crates()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = Bundles.Create("empty", "P", "1");

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*'empty' of P 1 holds no crates*");
    }

    [Fact]
    public void Check_says_which_bundle_could_not_open_its_archive()
    {
        //Arrange
        using var folder = new TempFolder();
        var bundle = new CrateBundle("gone", "P", "1", () => throw new FileNotFoundException("no such file"));

        //Act
        Action act = () => CrateBundleMerge.Check(Platform(folder), new[] { bundle });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*'gone' of P 1 could not be opened*no such file*");
    }

    [Fact]
    public void Check_refuses_a_missing_platform_archive()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        Action act = () => CrateBundleMerge.Check(folder.Combine("nowhere.zip"), null);

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void ExtractInto_writes_the_platforms_crates_and_one_copy_of_what_the_bundles_add()
    {
        //Arrange
        using var folder = new TempFolder();
        var wgpu = new Bundles.Crate("wgpu", "30.0.2");
        var first = Bundles.Create("graphics", "My.Graphics", "1.0", wgpu, WebSys);
        var second = Bundles.Create("animation", "My.Animation", "1.0", wgpu, new Bundles.Crate("lottie", "1.0.0"));
        var merge = CrateBundleMerge.Check(Platform(folder), new[] { first, second });

        //Act
        merge.ExtractInto(folder.Combine("vendor"));

        //Assert
        Directory.GetDirectories(folder.Combine("vendor")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal)
            .Should().Equal("lottie-1.0.0", "syn-2.0.1", "syn-3.0.1", "web-sys-0.3.1", "wgpu-30.0.2");
        File.Exists(folder.Combine("vendor/wgpu-30.0.2/src/lib.rs")).Should().BeTrue();
        File.Exists(folder.Combine("vendor/wgpu-30.0.2/.cargo-checksum.json")).Should().BeTrue();
    }

    [Fact]
    public void the_stamp_changes_when_a_bundle_or_its_origin_changes()
    {
        //Arrange
        using var folder = new TempFolder();
        var archive = Platform(folder);
        var wgpu = new Bundles.Crate("wgpu", "30.0.2");

        //Act
        var one = CrateBundleMerge.Check(archive, new[] { Bundles.Create("graphics", "My.Graphics", "1.0", wgpu) }).Stamp;
        var same = CrateBundleMerge.Check(archive, new[] { Bundles.Create("graphics", "My.Graphics", "1.0", wgpu) }).Stamp;
        var newerPackage = CrateBundleMerge.Check(archive, new[] { Bundles.Create("graphics", "My.Graphics", "1.1", wgpu) }).Stamp;
        var otherContent = CrateBundleMerge.Check(archive, new[] { Bundles.Create("graphics", "My.Graphics", "1.0", wgpu, new Bundles.Crate("extra", "1.0.0")) }).Stamp;

        //Assert
        same.Should().Be(one);
        newerPackage.Should().NotBe(one);
        otherContent.Should().NotBe(one);
    }

    [Theory]
    [InlineData("[package]\nname = \"a-b\"\nversion = \"1.2.3\"\n", "a-b", "1.2.3")]
    [InlineData("# comment\n[package]\nedition = \"2021\"\nversion=\"0.1.0\"\nname   =   \"x\"\n[dependencies]\nname = \"wrong\"\n", "x", "0.1.0")]
    [InlineData("[dependencies]\nname = \"wrong\"\nversion = \"9\"\n", null, null)]
    public void ReadPackage_reads_the_name_and_version_of_the_package_table(string manifest, string name, string version)
    {
        //Act
        var package = CrateBundleMerge.ReadPackage(manifest);

        //Assert
        package.Name.Should().Be(name);
        package.Version.Should().Be(version);
    }
}
