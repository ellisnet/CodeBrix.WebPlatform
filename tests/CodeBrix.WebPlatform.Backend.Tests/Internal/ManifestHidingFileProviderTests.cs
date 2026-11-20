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
        using var physical = new PhysicalFileProvider(root.Path);
        var provider = new ManifestHidingFileProvider(physical, root.Path);

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
        using var physical = new PhysicalFileProvider(root.Path);
        var provider = new ManifestHidingFileProvider(physical, root.Path);

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
        root.Write("app.js", "x");
        root.Write("app.js.br", "b");
        root.Write("app.js.gz", "g");
        root.Write("own.json.gz", "g");
        root.Write("sales/index.html", "x");
        root.Write("sales/index.html.br", "b");
        using var physical = new PhysicalFileProvider(root.Path);
        var provider = new ManifestHidingFileProvider(physical, root.Path);

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
        root.Write("app.js", "x");
        root.Write("app.js.br", "b");
        root.Write("own.json.gz", "g");
        using var physical = new PhysicalFileProvider(root.Path);
        var provider = new ManifestHidingFileProvider(physical, root.Path);

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
}
