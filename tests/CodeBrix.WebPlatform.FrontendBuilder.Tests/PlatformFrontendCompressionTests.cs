using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>CompressOutput and CompressibleExtensions as the public API sees them: the defaults, and BuildAsync alone reads them.</summary>
public class PlatformFrontendCompressionTests
{
    private static PlatformFrontend Create(PlatformFrontendOptions options, TempFolder folder)
    {
        if (!File.Exists(PlatformCrates.GetArchivePath(folder.Combine("build-program"))))
        {
            Frontends.WriteCratesArchive(folder);
        }

        return new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = Array.Empty<CodeBrix.WebPlatform.Core.Crates.CrateBundle>() };
    }

    [Fact]
    public void CompressOutput_is_not_set_by_default()
        => new PlatformFrontendOptions().CompressOutput.Should().BeNull();

    [Theory]
    [InlineData(null, FrontendConfiguration.Release, FrontendCompression.BrotliAndGzip)]
    [InlineData(null, FrontendConfiguration.Debug, FrontendCompression.None)]
    [InlineData(FrontendCompression.Gzip, FrontendConfiguration.Release, FrontendCompression.Gzip)]
    [InlineData(FrontendCompression.Gzip, FrontendConfiguration.Debug, FrontendCompression.Gzip)]
    [InlineData(FrontendCompression.None, FrontendConfiguration.Release, FrontendCompression.None)]
    [InlineData(FrontendCompression.BrotliAndGzip, FrontendConfiguration.Debug, FrontendCompression.BrotliAndGzip)]
    public void a_build_compresses_release_builds_and_not_debug_builds_unless_the_option_is_set(FrontendCompression? option, FrontendConfiguration configuration, FrontendCompression expected)
        => PlatformFrontend.EffectiveCompression(option, configuration).Should().Be(expected);

    [Fact]
    public void CompressibleExtensions_starts_as_the_platforms_defaults_and_can_be_changed()
    {
        //Arrange
        var options = new PlatformFrontendOptions();

        //Act
        options.CompressibleExtensions.Add(".dat");
        options.CompressibleExtensions.Remove(".csv");

        //Assert
        PlatformFrontendOptions.DefaultCompressibleExtensions.Should().Contain(new[] { ".wasm", ".js", ".css", ".html", ".gltf", ".glb", ".bin", ".hdr", ".ttf", ".otf", ".wgsl" });
        PlatformFrontendOptions.DefaultCompressibleExtensions.Should().NotContain(new[] { ".woff", ".woff2", ".png", ".jpg", ".zip", ".br", ".gz" });
        options.CompressibleExtensions.Should().Contain(".dat").And.NotContain(".csv");
        options.CompressibleExtensions.Contains(".WASM").Should().BeTrue("the set ignores case");
        new PlatformFrontendOptions().CompressibleExtensions.Should().BeEquivalentTo(PlatformFrontendOptions.DefaultCompressibleExtensions);
    }

    [Fact]
    public async Task BuildAsync_refuses_a_value_that_is_not_a_FrontendCompression_before_anything_else()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CompressOutput = (FrontendCompression)9;
        Directory.CreateDirectory(folder.Combine("backend"));
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.BuildAsync(backendProjectDirectory: folder.Combine("backend"), cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*CompressOutput*None, Brotli, Gzip or BrotliAndGzip*");
        Directory.Exists(folder.Combine("backend/frontend-built")).Should().BeFalse();
    }

    [Fact]
    public async Task BuildAsync_refuses_an_entry_of_CompressibleExtensions_that_is_not_an_extension_before_anything_else()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CompressibleExtensions.Add("glb");
        Directory.CreateDirectory(folder.Combine("backend"));
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.BuildAsync("app", FrontendConfiguration.Debug, folder.Combine("backend"), TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*CompressibleExtensions holds \"glb\"*");
        Directory.Exists(folder.Combine("backend/frontend-built")).Should().BeFalse();
    }

    [Fact]
    public async Task ServeAsync_does_not_read_CompressOutput()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CompressOutput = (FrontendCompression)9;
        options.CompressibleExtensions.Add("not an extension");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.ServeAsync(TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*dx and wasm-bindgen*"))
            .Which.Message.Should().NotContain("Compress");
    }
}
