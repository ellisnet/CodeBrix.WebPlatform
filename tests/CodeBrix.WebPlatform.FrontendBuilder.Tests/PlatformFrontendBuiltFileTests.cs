using System;
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

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>WriteBuiltFile: an additional file of the build program's own in frontend-built/, with its compressed copies.</summary>
public class PlatformFrontendBuiltFileTests
{
    private static readonly string Notices = string.Concat(Enumerable.Repeat("Some library - MIT License - Copyright (c) someone.\n", 400));

    private static string Frontend(TempFolder folder, string name)
    {
        folder.Write($"{name}/Cargo.toml", $"[package]\nname = \"{name}-frontend\"\nversion = \"0.1.0\"\nedition = \"2021\"\n");
        return folder.Combine(name);
    }

    /// <summary>A root application (folder "home") and "sales", with an existing backend project folder.</summary>
    private static PlatformFrontend Create(TempFolder folder, Action<PlatformFrontendOptions> change = null)
    {
        var options = new PlatformFrontendOptions { BackendAssemblyPath = Frontends.SampleBackendAssembly };
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Frontend(folder, "home") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Frontend(folder, "sales") });
        change?.Invoke(options);
        Directory.CreateDirectory(folder.Combine("backend"));
        return new PlatformFrontend(options, folder.Combine("build-program"));
    }

    private static byte[] Unbrotli(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    [Fact]
    public void WriteBuiltFile_writes_the_file_and_the_copies_a_release_build_writes()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);

        //Act
        var result = frontend.WriteBuiltFile(folder.Combine("backend"), "THIRD-PARTY-NOTICES.txt", Notices, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        var file = folder.Combine("backend/frontend-built/THIRD-PARTY-NOTICES.txt");
        File.ReadAllBytes(file).Should().Equal(new UTF8Encoding(false).GetBytes(Notices));
        Unbrotli(File.ReadAllBytes(file + ".br")).Should().Equal(File.ReadAllBytes(file));
        File.Exists(file + ".gz").Should().BeTrue();
        result.Compression.Should().Be(FrontendCompression.BrotliAndGzip);
        result.Files.Select(f => f.Path).Should().Equal("THIRD-PARTY-NOTICES.txt");
        result.RemovedCopyCount.Should().Be(0);
        File.ReadAllText(folder.Combine("backend/frontend-built/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void WriteBuiltFile_for_a_debug_build_writes_no_copies_and_removes_the_earlier_ones()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);
        frontend.WriteBuiltFile(folder.Combine("backend"), "legal/NOTICES.txt", Notices, FrontendConfiguration.Release, TestContext.Current.CancellationToken);

        //Act
        var result = frontend.WriteBuiltFile(folder.Combine("backend"), "legal\\NOTICES.txt", Notices + "changed", FrontendConfiguration.Debug, TestContext.Current.CancellationToken);

        //Assert
        File.ReadAllText(folder.Combine("backend/frontend-built/legal/NOTICES.txt")).Should().EndWith("changed");
        File.Exists(folder.Combine("backend/frontend-built/legal/NOTICES.txt.br")).Should().BeFalse();
        File.Exists(folder.Combine("backend/frontend-built/legal/NOTICES.txt.gz")).Should().BeFalse();
        result.Compression.Should().Be(FrontendCompression.None);
        result.Files.Should().BeEmpty();
        result.RemovedCopyCount.Should().Be(2);
    }

    [Fact]
    public void WriteBuiltFile_follows_the_options_compression_and_extensions()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder, o =>
        {
            o.CompressOutput = FrontendCompression.Gzip;
            o.CompressibleExtensions.Add(".dat");
        });

        //Act
        var data = frontend.WriteBuiltFile(folder.Combine("backend"), "data/table.dat", Notices, FrontendConfiguration.Debug, TestContext.Current.CancellationToken);
        var image = frontend.WriteBuiltFile(folder.Combine("backend"), "logo.png", new byte[2048], FrontendConfiguration.Debug, TestContext.Current.CancellationToken);

        //Assert
        data.Files.Select(f => f.Path).Should().Equal("data/table.dat");
        File.Exists(folder.Combine("backend/frontend-built/data/table.dat.gz")).Should().BeTrue();
        File.Exists(folder.Combine("backend/frontend-built/data/table.dat.br")).Should().BeFalse();
        image.Files.Should().BeEmpty();
        File.Exists(folder.Combine("backend/frontend-built/logo.png.gz")).Should().BeFalse();
        File.ReadAllBytes(folder.Combine("backend/frontend-built/logo.png")).Should().HaveCount(2048);
    }

    [Theory]
    [InlineData("", "blank")]
    [InlineData("/etc/notices.txt", "rooted")]
    [InlineData("../notices.txt", "'..' segment")]
    [InlineData("legal/../../notices.txt", "'..' segment")]
    [InlineData("./notices.txt", "'..' segment")]
    [InlineData("codebrix-frontends.toml", "frontend manifest")]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("legal/.gitignore", ".gitignore")]
    [InlineData("notices.txt.br", "compressed copy")]
    [InlineData("notices.txt.GZ", "compressed copy")]
    [InlineData("sales/notices.txt", "folder of the application 'sales'")]
    [InlineData("sales", "application 'sales''s folder")]
    public void WriteBuiltFile_refuses_a_path_the_build_owns_or_that_leaves_the_folder(string path, string expected)
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);

        //Act
        Action act = () => frontend.WriteBuiltFile(folder.Combine("backend"), path, "text", cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*" + expected + "*");
        Directory.Exists(folder.Combine("backend/frontend-built")).Should().BeFalse("nothing is written when the path is refused");
    }

    [Fact]
    public void WriteBuiltFile_refuses_a_file_of_the_root_applications_site_and_a_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);
        folder.Write("home/target/dx/home-frontend/debug/web/public/assets/style.css", "dx's");
        folder.Write("backend/frontend-built/legal/old.txt", "x");

        //Act
        Action site = () => frontend.WriteBuiltFile(folder.Combine("backend"), "assets/style.css", "mine", cancellationToken: TestContext.Current.CancellationToken);
        Action directory = () => frontend.WriteBuiltFile(folder.Combine("backend"), "legal", "mine", cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        site.Should().Throw<FrontendBuildException>().WithMessage("*'assets/style.css' is a file of the root application's built site*");
        directory.Should().Throw<FrontendBuildException>().WithMessage("*'legal' is a folder*");
    }

    [Fact]
    public void WriteBuiltFile_refuses_a_missing_backend_project_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);

        //Act
        Action missing = () => frontend.WriteBuiltFile(folder.Combine("elsewhere"), "notices.txt", "text", cancellationToken: TestContext.Current.CancellationToken);
        Action none = () => frontend.WriteBuiltFile(null, "notices.txt", "text", cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        missing.Should().Throw<FrontendBuildException>().WithMessage("*elsewhere' does not exist*");
        none.Should().Throw<FrontendBuildException>().WithMessage("*backend project folder*null*");
    }

    [Fact]
    public void an_additional_file_survives_a_named_applications_build_but_not_a_root_or_full_build()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(folder);
        var built = folder.Combine("backend/frontend-built");
        var applications = FrontendApplicationSet.FromNames(new[] { null, "sales" });
        folder.Write("sites/root/index.html", "root");
        folder.Write("sites/sales/index.html", "sales");
        var notices = Path.Combine(built, "legal", "NOTICES.txt");

        //Act
        frontend.WriteBuiltFile(folder.Combine("backend"), "legal/NOTICES.txt", Notices, cancellationToken: TestContext.Current.CancellationToken);
        BuiltFrontendWriter.WriteOne(built, applications, FrontendApplication.Named("sales"), folder.Combine("sites/sales"));
        var afterNamed = File.Exists(notices) && File.Exists(notices + ".br");
        BuiltFrontendWriter.WriteOne(built, applications, FrontendApplication.Default, folder.Combine("sites/root"));
        var afterRoot = File.Exists(notices) || File.Exists(notices + ".br");
        frontend.WriteBuiltFile(folder.Combine("backend"), "legal/NOTICES.txt", Notices, cancellationToken: TestContext.Current.CancellationToken);
        BuiltFrontendWriter.Write(built, new[] { (FrontendApplication.Default, folder.Combine("sites/root")), (FrontendApplication.Named("sales"), folder.Combine("sites/sales")) });
        var afterFull = File.Exists(notices) || File.Exists(notices + ".br");

        //Assert
        afterNamed.Should().BeTrue();
        afterRoot.Should().BeFalse();
        afterFull.Should().BeFalse();
    }
}
