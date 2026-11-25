using System.Linq;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.FileProviders;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class ManifestHidingFileProviderTests
{
    [Theory]
    [InlineData("/codebrix-frontends.toml", true)]
    [InlineData("codebrix-frontends.toml", true)]
    [InlineData("//codebrix-frontends.toml", true)]
    [InlineData("/CODEBRIX-FRONTENDS.TOML", true)]
    [InlineData("/codebrix-frontends.toml.", true)]
    [InlineData("/codebrix-frontends.toml. . ", true)]
    [InlineData("/codebrix-frontends.toml::$DATA", true)]
    [InlineData("\\codebrix-frontends.toml", true)]
    [InlineData("/sales/codebrix-frontends.toml", false)]
    [InlineData("/codebrix-frontends.toml.bak", false)]
    [InlineData("/codebrix-frontends", false)]
    [InlineData("/index.html", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsManifestAddress_recognises_every_spelling_of_the_manifest_at_the_web_root(string subpath, bool expected)
        => ManifestHidingFileProvider.IsManifestAddress(subpath).Should().Be(expected);

    [Fact]
    public void the_manifest_is_not_found_and_other_files_are()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
        using var physical = new PhysicalFileProvider(root.BuiltPath);
        var provider = new ManifestHidingFileProvider(physical, root.BuiltPath);

        //Act
        var manifest = provider.GetFileInfo("/" + FrontendManifest.FileName);
        var dotted = provider.GetFileInfo("/./" + FrontendManifest.FileName);
        var index = provider.GetFileInfo("/index.html");

        //Assert
        physical.GetFileInfo("/" + FrontendManifest.FileName).Exists.Should().BeTrue();
        manifest.Exists.Should().BeFalse();
        dotted.Exists.Should().BeFalse();
        index.Exists.Should().BeTrue();
    }

    [Fact]
    public void the_manifest_is_left_out_of_the_web_roots_listing()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
        using var physical = new PhysicalFileProvider(root.BuiltPath);
        var provider = new ManifestHidingFileProvider(physical, root.BuiltPath);

        //Act
        var names = provider.GetDirectoryContents("/").Select(f => f.Name).ToArray();

        //Assert
        names.Should().Contain("index.html");
        names.Should().NotContain(FrontendManifest.FileName);
    }

    [Theory]
    [InlineData("/app.js.br", true)]
    [InlineData("/app.js.gz", true)]
    [InlineData("app.js.br", true)]
    [InlineData("/app.js.br.", true)]
    [InlineData("/app.js.br::$DATA", true)]
    [InlineData("\\app.js.gz", true)]
    [InlineData("/sales/index.html.br", true)]
    [InlineData("/app.js", false)]
    [InlineData("/own.json.gz", false)]
    [InlineData("/missing.js.br", false)]
    [InlineData("/sales.br", false)]
    [InlineData("/.br", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCopyAddress_is_true_for_a_br_or_gz_file_beside_its_file(string subpath, bool expected)
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteBuilt("app.js", "x");
        root.WriteBuilt("app.js.br", "b");
        root.WriteBuilt("app.js.gz", "g");
        root.WriteBuilt("own.json.gz", "g");
        root.WriteBuilt("sales/index.html", "x");
        root.WriteBuilt("sales/index.html.br", "b");
        using var physical = new PhysicalFileProvider(root.BuiltPath);
        var provider = new ManifestHidingFileProvider(physical, root.BuiltPath);

        //Act
        var copy = provider.IsCopyAddress(subpath);

        //Assert
        copy.Should().Be(expected);
    }

    [Fact]
    public void a_copy_is_not_found_by_its_name_or_listed_but_GetCopy_finds_it()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteBuilt("app.js", "x");
        root.WriteBuilt("app.js.br", "b");
        root.WriteBuilt("own.json.gz", "g");
        using var physical = new PhysicalFileProvider(root.BuiltPath);
        var provider = new ManifestHidingFileProvider(physical, root.BuiltPath);

        //Act
        var byName = provider.GetFileInfo("/app.js.br");
        var copy = provider.GetCopy("/app.js", ".br");
        var none = provider.GetCopy("/app.js", ".gz");
        var names = provider.GetDirectoryContents("/").Select(f => f.Name).ToArray();

        //Assert
        byName.Exists.Should().BeFalse();
        copy.Exists.Should().BeTrue();
        none.Exists.Should().BeFalse();
        names.Should().Contain("app.js");
        names.Should().Contain("own.json.gz");
        names.Should().NotContain("app.js.br");
    }

    [Theory]
    [InlineData("/.gitignore", false)]
    [InlineData(".gitignore", false)]
    [InlineData("/.GITIGNORE", false)]
    [InlineData("/.gitignore. ", false)]
    [InlineData("/.gitignore::$DATA", false)]
    public void the_folders_gitignore_is_not_found_and_not_listed(string subpath, bool expected)
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
        root.WriteBuilt(".gitignore", "*");
        using var physical = new PhysicalFileProvider(root.BuiltPath, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);
        var provider = new ManifestHidingFileProvider(physical, root.BuiltPath);

        //Act
        var info = provider.GetFileInfo(subpath);
        var names = provider.GetDirectoryContents("/").Select(f => f.Name).ToArray();

        //Assert
        physical.GetFileInfo("/.gitignore").Exists.Should().BeTrue();
        info.Exists.Should().Be(expected);
        names.Should().NotContain(".gitignore");
        names.Should().Contain("index.html");
    }
}
