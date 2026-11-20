using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>CompressOutput as the public API sees it: the default, and BuildAsync alone reads it.</summary>
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
    public void CompressOutput_is_Brotli_and_gzip_by_default()
        => new PlatformFrontendOptions().CompressOutput.Should().Be(FrontendCompression.BrotliAndGzip);

    [Fact]
    public async Task BuildAsync_refuses_a_value_that_is_not_a_FrontendCompression_before_anything_else()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CompressOutput = (FrontendCompression)9;
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.BuildAsync(webRootDirectory: folder.Combine("wwwroot"), cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*CompressOutput*None, Brotli, Gzip or BrotliAndGzip*");
        Directory.Exists(folder.Combine("wwwroot")).Should().BeFalse();
    }

    [Fact]
    public async Task ServeAsync_does_not_read_CompressOutput()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CompressOutput = (FrontendCompression)9;
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.ServeAsync(TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*dx and wasm-bindgen*"))
            .Which.Message.Should().NotContain("CompressOutput");
    }
}
