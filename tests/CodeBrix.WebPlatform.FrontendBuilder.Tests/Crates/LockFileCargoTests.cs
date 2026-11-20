using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

/// <summary>
/// The lock-file marker and the repair on the INSTALLED cargo, with tiny made-up crates in
/// vendored form. That cargo keeps a comment on the third line of Cargo.lock - on an ordinary
/// build and when it rewrites the file - is observed behaviour, not a documented promise: these
/// tests are the guard. Skipped on a machine without cargo.
/// </summary>
public class LockFileCargoTests
{
    private const string NoCargo = "cargo is not installed on this machine.";

    private static readonly Bundles.Crate Keep = new Bundles.Crate("keep", "2.0.0");
    private static readonly Bundles.Crate Other = new Bundles.Crate("other", "1.0.0");
    private static readonly Bundles.Crate Bundled10 = new Bundles.Crate("bundled", "1.0.0");
    private static readonly Bundles.Crate Bundled11 = new Bundles.Crate("bundled", "1.1.0");

    private static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "src", "CodeBrix.WebPlatform.FrontendBuilder", "Rust", "platform-crates", "Cargo.toml")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
        }
    }

    private static void WriteConfiguration(string frontend, string vendor)
        => CargoConfiguration.Write(Path.Combine(frontend, ".cargo", "config.toml"), CargoConfiguration.Create(CrateMode.PlatformCratesOnly, CargoConfiguration.VendorPath(frontend, vendor)));

    private static async Task CargoMustSucceed(string directory, params string[] arguments)
    {
        var run = await VendoredCrates.CargoAsync(directory, TestContext.Current.CancellationToken, arguments);
        run.Succeeded.Should().BeTrue("`cargo " + string.Join(" ", arguments) + "` should succeed, but said: " + run.Output);
    }

    [Fact]
    public async Task cargo_keeps_the_marker_on_the_third_line_on_an_ordinary_build_and_when_it_rewrites_the_lock_file()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, NoCargo);

        //Arrange
        using var folder = new TempFolder();
        ZipFile.ExtractToDirectory(new MemoryStream(VendoredCrates.Archive(Keep, Other)), folder.Combine("vendor"));
        var frontend = VendoredCrates.WriteFrontend(folder, "frontend", ("keep", "2"));
        WriteConfiguration(frontend, folder.Combine("vendor"));
        await CargoMustSucceed(frontend, "generate-lockfile", "--offline");
        var lockFile = Path.Combine(frontend, "Cargo.lock");
        File.WriteAllText(lockFile, LockFileRules.AddMarker(File.ReadAllText(lockFile)));
        var marked = File.ReadAllBytes(lockFile);

        //Act
        await CargoMustSucceed(frontend, "check", "--offline");
        var afterOrdinaryBuild = File.ReadAllBytes(lockFile);
        VendoredCrates.WriteFrontend(folder, "frontend", ("keep", "2"), ("other", "1"));
        await CargoMustSucceed(frontend, "check", "--offline");
        var afterRewrite = File.ReadAllText(lockFile);
        await CargoMustSucceed(frontend, "update", "--offline", "-p", "keep");
        var afterUpdate = File.ReadAllText(lockFile);

        //Assert
        afterOrdinaryBuild.Should().Equal(marked);
        afterRewrite.Should().Contain("name = \"other\"");
        afterRewrite.Split('\n')[2].Should().Be(PlatformCrates.LockFileMarker);
        afterUpdate.Split('\n')[2].Should().Be(PlatformCrates.LockFileMarker);
        LockFileRules.HasMarker(afterUpdate).Should().BeTrue();
    }

    [Fact]
    public async Task the_platforms_lock_file_with_the_marker_is_accepted_with_locked_when_nothing_changed()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, NoCargo);

        //Arrange
        using var folder = new TempFolder();
        var source = Path.Combine(RepositoryRoot, "src", "CodeBrix.WebPlatform.FrontendBuilder", "Rust");
        var crateSet = folder.Combine("platform-crates");
        Directory.CreateDirectory(Path.Combine(crateSet, "src"));
        File.Copy(Path.Combine(source, "platform-crates", "Cargo.toml"), Path.Combine(crateSet, "Cargo.toml"));
        File.Copy(Path.Combine(source, "platform-crates", "src", "lib.rs"), Path.Combine(crateSet, "src", "lib.rs"));
        SharedRustCrate.ExtractTo(folder.Combine("codebrix-webplatform"));
        WriteConfiguration(crateSet, Path.Combine(source, "vendor"));
        var lockFile = Path.Combine(crateSet, "Cargo.lock");
        File.WriteAllText(lockFile, LockFileRules.AddMarker(PlatformCrates.LockFileText));
        var marked = File.ReadAllBytes(lockFile);

        //Act
        await CargoMustSucceed(crateSet, "metadata", "--offline", "--locked", "--format-version", "1");

        //Assert
        File.ReadAllBytes(lockFile).Should().Equal(marked);
    }

    private static async Task<(PlatformFrontendOptions Options, string LockFile, string LockedText)> MaterializeWithFirstBundleAsync(TempFolder folder, bool keepMarker)
    {
        VendoredCrates.WritePlatformArchive(folder, Keep);
        var frontend = VendoredCrates.WriteFrontend(folder, "frontend", ("bundled", "1"), ("keep", "2"));
        var options = new PlatformFrontendOptions { FrontendDirectory = frontend, BackendAssemblyPath = Frontends.SampleBackendAssembly };
        var first = new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = new[] { VendoredCrates.Bundle("tiny", "My.Tiny", "1.0", Bundled10) } };
        (await first.MaterializeAsync(TestContext.Current.CancellationToken)).LockFileWritten.Should().BeTrue();

        // cargo resolves the starting lock file down to what this frontend uses.
        await CargoMustSucceed(frontend, "metadata", "--offline", "--format-version", "1");
        var lockFile = Path.Combine(frontend, "Cargo.lock");
        var text = File.ReadAllText(lockFile);
        text.Should().Contain("name = \"bundled\"\nversion = \"1.0.0\"");
        text.Split('\n')[2].Should().Be(PlatformCrates.LockFileMarker);
        if (!keepMarker)
        {
            text = text.Replace(PlatformCrates.LockFileMarker + "\n", string.Empty, StringComparison.Ordinal);
            File.WriteAllText(lockFile, text);
        }

        return (options, lockFile, text);
    }

    private static PlatformFrontend WithSecondBundle(TempFolder folder, PlatformFrontendOptions options)
        => new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = new CrateBundle[] { VendoredCrates.Bundle("tiny", "My.Tiny", "1.1", Bundled11) } };

    [Fact]
    public async Task MaterializeAsync_brings_exactly_the_moved_entry_of_a_marked_lock_file_in_step()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, NoCargo);

        //Arrange
        using var folder = new TempFolder();
        var (options, lockFile, before) = await MaterializeWithFirstBundleAsync(folder, keepMarker: true);

        //Act
        var result = await WithSecondBundle(folder, options).MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileMarked.Should().BeTrue();
        result.LockFileRepairs.Should().ContainSingle().Which.ToString().Should().Be("bundled 1.0.0 -> 1.1.0");
        result.StaleLockFileEntries.Should().BeEmpty();
        result.LockFileWarning.Should().BeEmpty();
        result.WasUpToDate.Should().BeFalse();
        var after = File.ReadAllText(lockFile);
        after.Split('\n')[2].Should().Be(PlatformCrates.LockFileMarker);
        after.Should().Be(before.Replace("name = \"bundled\"\nversion = \"1.0.0\"", "name = \"bundled\"\nversion = \"1.1.0\"", StringComparison.Ordinal));
        await CargoMustSucceed(options.FrontendDirectory, "metadata", "--offline", "--locked", "--format-version", "1");
    }

    [Fact]
    public async Task MaterializeAsync_never_changes_a_lock_file_without_the_marker_and_gives_the_command_that_does()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, NoCargo);

        //Arrange
        using var folder = new TempFolder();
        var (options, lockFile, before) = await MaterializeWithFirstBundleAsync(folder, keepMarker: false);
        var bytes = File.ReadAllBytes(lockFile);

        //Act
        var result = await WithSecondBundle(folder, options).MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileMarked.Should().BeFalse();
        result.LockFileRepairs.Should().BeEmpty();
        result.StaleLockFileEntries.Should().Equal("bundled 1.0.0");
        result.LockFileUpdateCommand.Should().Be("cargo update --offline -p bundled");
        result.LockFileWarning.Should().Contain("    cargo update --offline -p bundled");
        File.ReadAllBytes(lockFile).Should().Equal(bytes);
        before.Should().NotContain(PlatformCrates.LockFileMarkerPrefix);
        await CargoMustSucceed(options.FrontendDirectory, "update", "--offline", "-p", "bundled");
        await CargoMustSucceed(options.FrontendDirectory, "metadata", "--offline", "--locked", "--format-version", "1");
    }
}
