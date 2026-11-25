using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

/// <summary>
/// A frontend with a LOCAL PATH CRATE beside it (<c>shared = { path = "../shared" }</c>, a crate
/// of the application's own whose dependencies are all vendored) in
/// <see cref="CrateMode.PlatformCratesOnly"/>: it materializes, its lock file passes the
/// checks, and the installed cargo builds it offline from the vendor folder (which holds its own
/// .gitignore beside the stamp). Skipped on a machine without cargo.
/// </summary>
public class PathCrateTests
{
    private static readonly Bundles.Crate Keep = new Bundles.Crate("keep", "2.0.0");

    private static async Task CargoMustSucceed(string directory, params string[] arguments)
    {
        var run = await VendoredCrates.CargoAsync(directory, TestContext.Current.CancellationToken, arguments);
        run.Succeeded.Should().BeTrue("`cargo " + string.Join(" ", arguments) + "` should succeed, but said: " + run.Output);
    }

    [Fact]
    public async Task a_frontend_with_a_path_crate_beside_it_materializes_passes_the_lock_file_checks_and_builds_offline()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, "cargo is not installed on this machine.");

        //Arrange
        using var folder = new TempFolder();
        VendoredCrates.WritePlatformArchive(folder, Keep);
        folder.Write("shared/Cargo.toml", "[package]\nname = \"shared\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\nkeep = \"2\"\n");
        folder.Write("shared/src/lib.rs", "pub fn keep_version() -> &'static str { keep::version() }\n");
        folder.Write("frontend/Cargo.toml", "[package]\nname = \"path-crate-frontend\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\nshared = { path = \"../shared\" }\n");
        folder.Write("frontend/src/lib.rs", "pub fn version() -> &'static str { shared::keep_version() }\n");
        var options = new PlatformFrontendOptions { FrontendDirectory = folder.Combine("frontend"), BackendAssemblyPath = Frontends.SampleBackendAssembly };
        var frontend = new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = Array.Empty<CodeBrix.WebPlatform.Core.Crates.CrateBundle>() };

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        await CargoMustSucceed(options.FrontendDirectory, "build", "--offline");
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        Action beforeDx = () => frontend.RequireLockFilesInStep();
        await CargoMustSucceed(options.FrontendDirectory, "build", "--offline", "--locked");

        //Assert
        first.LockFileWritten.Should().BeTrue();
        second.LockFileMarked.Should().BeTrue();
        second.StaleLockFileEntries.Should().BeEmpty();
        second.LockFileRepairs.Should().BeEmpty();
        second.LockFileWarning.Should().BeEmpty();
        beforeDx.Should().NotThrow();
        var lockFile = File.ReadAllText(folder.Combine("frontend/Cargo.lock"));
        lockFile.Should().Contain("name = \"shared\"\nversion = \"0.1.0\"\ndependencies = [\n \"keep\",\n]");
        lockFile.Should().Contain("name = \"keep\"\nversion = \"2.0.0\"\nsource = \"registry+https://github.com/rust-lang/crates.io-index\"");
        File.Exists(Path.Combine(options.VendorDirectory, BuildOutputFolder.IgnoreFileName)).Should().BeTrue("cargo's directory source skips the dot-file");
        File.Exists(folder.Combine("shared/.gitignore")).Should().BeFalse("the path crate is the developer's own folder");
        Directory.Exists(folder.Combine("shared/.cargo")).Should().BeFalse();
    }
}
