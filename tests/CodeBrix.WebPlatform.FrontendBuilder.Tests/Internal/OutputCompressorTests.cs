using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Internal;

public class OutputCompressorTests
{
    private static readonly IReadOnlyCollection<string> Defaults = OutputCompressor.RequireExtensions(PlatformFrontendOptions.DefaultCompressibleExtensions);

    private static readonly string Text = string.Concat(Enumerable.Repeat("function hello() { return 'hello, world'; }\n", 300));

    /// <summary>A made-up built site: compressible files, files of compressed types, and a tiny file.</summary>
    private static string[] WriteSite(TempFolder folder, string root = "site")
    {
        var files = new[]
        {
            "index.html", "wasm/app_bg.wasm", "wasm/app.js", "assets/tailwind-dxh12.css", "assets/data.json", "assets/logo.svg",
            "notes.txt", "assets/photo.png", "assets/photo.jpg", "assets/font.woff2", "favicon.ico", "assets/bundle.zip", "tiny.js",
        };
        foreach (var file in files)
        {
            folder.Write(root + "/" + file, file == "tiny.js" ? "a" : file + "\n" + Text);
        }

        return files;
    }

    private static byte[] Read(TempFolder folder, string path) => File.ReadAllBytes(folder.Combine(path));

    private static byte[] Unbrotli(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Ungzip(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    [Theory]
    [InlineData("app_bg.wasm", true)]
    [InlineData("app.js", true)]
    [InlineData("module.mjs", true)]
    [InlineData("tailwind.css", true)]
    [InlineData("index.html", true)]
    [InlineData("data.json", true)]
    [InlineData("app.js.map", true)]
    [InlineData("logo.svg", true)]
    [InlineData("THIRD-PARTY-NOTICES.txt", true)]
    [InlineData("shader.wgsl", true)]
    [InlineData("model.gltf", true)]
    [InlineData("model.glb", true)]
    [InlineData("buffers.bin", true)]
    [InlineData("sky.hdr", true)]
    [InlineData("font.ttf", true)]
    [InlineData(".gitignore", false)]
    [InlineData("INDEX.HTML", true)]
    [InlineData("photo.png", false)]
    [InlineData("photo.jpg", false)]
    [InlineData("anim.gif", false)]
    [InlineData("photo.webp", false)]
    [InlineData("favicon.ico", false)]
    [InlineData("font.woff", false)]
    [InlineData("font.woff2", false)]
    [InlineData("bundle.zip", false)]
    [InlineData("app.js.gz", false)]
    [InlineData("app.js.br", false)]
    [InlineData("README", false)]
    [InlineData("", false)]
    public void IsCompressible_takes_text_and_webassembly_and_never_what_is_compressed_already(string name, bool expected)
        => OutputCompressor.IsCompressible(name, Defaults).Should().Be(expected);

    [Fact]
    public void Compress_writes_both_copies_beside_each_compressible_file_and_none_beside_the_others()
    {
        //Arrange
        using var folder = new TempFolder();
        var files = WriteSite(folder);

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), files, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Assert
        foreach (var file in new[] { "index.html", "wasm/app_bg.wasm", "wasm/app.js", "assets/tailwind-dxh12.css", "assets/data.json", "assets/logo.svg", "notes.txt" })
        {
            var original = Read(folder, "site/" + file);
            Unbrotli(Read(folder, "site/" + file + ".br")).Should().Equal(original);
            Ungzip(Read(folder, "site/" + file + ".gz")).Should().Equal(original);
        }

        foreach (var file in new[] { "assets/photo.png", "assets/photo.jpg", "assets/font.woff2", "favicon.ico", "assets/bundle.zip" })
        {
            File.Exists(folder.Combine("site/" + file + ".br")).Should().BeFalse();
            File.Exists(folder.Combine("site/" + file + ".gz")).Should().BeFalse();
        }

        result.Compression.Should().Be(FrontendCompression.BrotliAndGzip);
        result.Files.Select(f => f.Path).Should().Equal("assets/data.json", "assets/logo.svg", "assets/tailwind-dxh12.css", "index.html", "notes.txt", "tiny.js", "wasm/app.js", "wasm/app_bg.wasm");
        result.BrotliFileCount.Should().Be(7);
        result.GzipFileCount.Should().Be(7);
    }

    [Fact]
    public void Compress_writes_a_copy_only_when_it_is_smaller_than_the_file()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("site/tiny.js", "a");
        var random = new byte[4096];
        new Random(7).NextBytes(random);
        File.WriteAllBytes(folder.Combine("site/noise.wasm"), random);

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), new[] { "tiny.js", "noise.wasm" }, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Assert
        Directory.GetFiles(folder.Combine("site")).Select(Path.GetFileName).Should().BeEquivalentTo(new[] { "tiny.js", "noise.wasm" });
        result.Files.Should().OnlyContain(f => f.BrotliBytes == null && f.GzipBytes == null);
        result.BrotliBytes.Should().Be(result.OriginalBytes);
        result.GzipBytes.Should().Be(result.OriginalBytes);
    }

    [Theory]
    [InlineData(FrontendCompression.None, false, false)]
    [InlineData(FrontendCompression.Brotli, true, false)]
    [InlineData(FrontendCompression.Gzip, false, true)]
    [InlineData(FrontendCompression.BrotliAndGzip, true, true)]
    public void Compress_writes_the_copies_each_option_asks_for(FrontendCompression compression, bool brotli, bool gzip)
    {
        //Arrange
        using var folder = new TempFolder();
        var files = WriteSite(folder);

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), files, compression, Defaults, TestContext.Current.CancellationToken);

        //Assert
        File.Exists(folder.Combine("site/wasm/app_bg.wasm.br")).Should().Be(brotli);
        File.Exists(folder.Combine("site/wasm/app_bg.wasm.gz")).Should().Be(gzip);
        result.Compression.Should().Be(compression);
        result.BrotliFileCount.Should().Be(brotli ? 7 : 0);
        result.GzipFileCount.Should().Be(gzip ? 7 : 0);
        result.Files.Count.Should().Be(compression == FrontendCompression.None ? 0 : 8);
    }

    [Fact]
    public void Compress_reports_the_sizes_before_and_after_and_the_time()
    {
        //Arrange
        using var folder = new TempFolder();
        var files = WriteSite(folder);

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), files, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Assert
        var wasm = result.Files.Single(f => f.Path == "wasm/app_bg.wasm");
        wasm.OriginalBytes.Should().Be(new FileInfo(folder.Combine("site/wasm/app_bg.wasm")).Length);
        wasm.BrotliBytes.Should().Be(new FileInfo(folder.Combine("site/wasm/app_bg.wasm.br")).Length);
        wasm.GzipBytes.Should().Be(new FileInfo(folder.Combine("site/wasm/app_bg.wasm.gz")).Length);
        wasm.BrotliBytes.Should().BeLessThan(wasm.OriginalBytes);
        wasm.ToString().Should().Be($"wasm/app_bg.wasm {wasm.OriginalBytes} bytes, br {wasm.BrotliBytes}, gz {wasm.GzipBytes}");
        result.OriginalBytes.Should().Be(result.Files.Sum(f => f.OriginalBytes));
        result.BrotliBytes.Should().Be(result.Files.Sum(f => f.BrotliBytes ?? f.OriginalBytes));
        result.GzipBytes.Should().Be(result.Files.Sum(f => f.GzipBytes ?? f.OriginalBytes));
        result.BrotliBytes.Should().BeLessThan(result.GzipBytes);
        result.GzipBytes.Should().BeLessThan(result.OriginalBytes);
        result.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
        result.RemovedCopyCount.Should().Be(0);
    }

    [Fact]
    public void Compress_gives_the_same_bytes_for_the_same_files()
    {
        //Arrange
        using var folder = new TempFolder();
        var files = WriteSite(folder, "one");
        WriteSite(folder, "two");

        //Act
        OutputCompressor.Compress(folder.Combine("one"), files, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);
        OutputCompressor.Compress(folder.Combine("two"), files, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Assert
        foreach (var file in files.Where(f => OutputCompressor.IsCompressible(f, Defaults)).Where(f => f != "tiny.js"))
        {
            Read(folder, "two/" + file + ".br").Should().Equal(Read(folder, "one/" + file + ".br"));
            Read(folder, "two/" + file + ".gz").Should().Equal(Read(folder, "one/" + file + ".gz"));
        }

        Read(folder, "one/index.html.gz")[9].Should().Be(0xff, "the gzip header names no operating system, so every machine writes the same bytes");
    }

    [Fact]
    public void Compress_replaces_dxs_own_copies_and_removes_those_the_option_does_not_ask_for()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("site/app.js", Text);
        folder.Write("site/app.js.br", "dx's copy");
        folder.Write("site/app.js.gz", "an old copy");
        folder.Write("site/own.json.gz", "a file of the application's own");

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), new[] { "app.js", "app.js.br", "app.js.gz", "own.json.gz" }, FrontendCompression.Brotli, Defaults, TestContext.Current.CancellationToken);

        //Assert
        Unbrotli(Read(folder, "site/app.js.br")).Should().Equal(Encoding.UTF8.GetBytes(Text));
        File.Exists(folder.Combine("site/app.js.gz")).Should().BeFalse();
        File.ReadAllText(folder.Combine("site/own.json.gz")).Should().Be("a file of the application's own");
        result.Files.Select(f => f.Path).Should().Equal("app.js");
        result.RemovedCopyCount.Should().Be(1);
    }

    [Fact]
    public void Compress_with_None_leaves_dxs_own_copies_as_they_are()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("site/app.js", Text);
        folder.Write("site/app.js.br", "dx's copy");

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), new[] { "app.js", "app.js.br" }, FrontendCompression.None, Defaults, TestContext.Current.CancellationToken);

        //Assert
        File.ReadAllText(folder.Combine("site/app.js.br")).Should().Be("dx's copy");
        result.Files.Should().BeEmpty();
        result.RemovedCopyCount.Should().Be(0);
    }

    [Theory]
    [InlineData(FrontendCompression.None)]
    [InlineData(FrontendCompression.BrotliAndGzip)]
    public void Compress_removes_stale_copies_of_files_that_no_longer_exist(FrontendCompression compression)
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("site/app.js", Text);
        folder.Write("site/wasm/new.wasm", Text);
        folder.Write("site/wasm/old.wasm.br", "stale");
        folder.Write("site/wasm/old.wasm.gz", "stale");
        folder.Write("site/old.js.br", "stale");
        folder.Write("site/other-folder/kept.js.br", "not a folder of the application's files");

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), new[] { "app.js", "wasm/new.wasm" }, compression, Defaults, TestContext.Current.CancellationToken);

        //Assert
        File.Exists(folder.Combine("site/wasm/old.wasm.br")).Should().BeFalse();
        File.Exists(folder.Combine("site/wasm/old.wasm.gz")).Should().BeFalse();
        File.Exists(folder.Combine("site/old.js.br")).Should().BeFalse();
        File.Exists(folder.Combine("site/other-folder/kept.js.br")).Should().BeTrue();
        result.RemovedCopyCount.Should().Be(3);
    }

    [Fact]
    public void a_second_build_leaves_no_copy_of_a_file_it_no_longer_has()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("frontend-built");
        folder.Write("first/index.html", Text);
        folder.Write("first/assets/old-dxh1.js", Text);
        folder.Write("second/index.html", Text + "changed");
        folder.Write("second/assets/new-dxh2.js", Text);
        BuiltFrontendWriter.Write(built, new[] { (FrontendApplication.Named("sales"), folder.Combine("first")) });
        OutputCompressor.Compress(Path.Combine(built, "sales"), new[] { "index.html", "assets/old-dxh1.js" }, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Act
        BuiltFrontendWriter.Write(built, new[] { (FrontendApplication.Named("sales"), folder.Combine("second")) });
        OutputCompressor.Compress(Path.Combine(built, "sales"), new[] { "index.html", "assets/new-dxh2.js" }, FrontendCompression.BrotliAndGzip, Defaults, TestContext.Current.CancellationToken);

        //Assert
        Directory.GetFiles(Path.Combine(built, "sales"), "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(Path.Combine(built, "sales"), f).Replace('\\', '/'))
            .Should().BeEquivalentTo(new[] { "index.html", "index.html.br", "index.html.gz", "assets/new-dxh2.js", "assets/new-dxh2.js.br", "assets/new-dxh2.js.gz" });
        Ungzip(Read(folder, "frontend-built/sales/index.html.gz")).Should().Equal(Encoding.UTF8.GetBytes(Text + "changed"));
    }

    [Fact]
    public void Compress_of_the_root_application_leaves_the_named_applications_and_the_gitignore_alone()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("frontend-built");
        folder.Write("root/index.html", Text);
        folder.Write("sales/index.html", Text);
        BuiltFrontendWriter.Write(built, new[] { (FrontendApplication.Default, folder.Combine("root")), (FrontendApplication.Named("sales"), folder.Combine("sales")) });
        var everything = OutputCompressor.RequireExtensions(new[] { ".html", ".gitignore" });

        //Act
        var result = OutputCompressor.Compress(built, new[] { "index.html", ".gitignore" }, FrontendCompression.BrotliAndGzip, everything, TestContext.Current.CancellationToken);

        //Assert
        result.Files.Select(f => f.Path).Should().Equal("index.html");
        File.Exists(folder.Combine("frontend-built/index.html.br")).Should().BeTrue();
        File.Exists(folder.Combine("frontend-built/.gitignore.br")).Should().BeFalse();
        File.Exists(folder.Combine("frontend-built/.gitignore.gz")).Should().BeFalse();
        File.Exists(folder.Combine("frontend-built/sales/index.html.br")).Should().BeFalse();
    }

    [Fact]
    public void Compress_takes_the_extensions_it_is_given()
    {
        //Arrange
        using var folder = new TempFolder();
        var files = WriteSite(folder);
        var onlyWasm = OutputCompressor.RequireExtensions(new[] { ".WASM" });

        //Act
        var result = OutputCompressor.Compress(folder.Combine("site"), files, FrontendCompression.Brotli, onlyWasm, TestContext.Current.CancellationToken);

        //Assert
        result.Files.Select(f => f.Path).Should().Equal("wasm/app_bg.wasm");
        File.Exists(folder.Combine("site/wasm/app.js.br")).Should().BeFalse();
    }

    [Theory]
    [InlineData("wasm")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData(".")]
    [InlineData(".tar.gz")]
    [InlineData("./x")]
    [InlineData(".br")]
    [InlineData(".GZ")]
    public void RequireExtensions_refuses_what_is_not_an_extension_or_is_a_copy(string extension)
    {
        //Act
        Action act = () => OutputCompressor.RequireExtensions(new[] { ".js", extension });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*CompressibleExtensions*");
    }
}
