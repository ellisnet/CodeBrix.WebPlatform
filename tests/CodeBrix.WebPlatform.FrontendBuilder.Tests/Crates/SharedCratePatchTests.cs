using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

/// <summary>
/// The patch table of the cargo configuration MaterializeAsync writes: a path crate of the
/// application's own that names the shared crate BY VERSION (<c>codebrix-webplatform = "*"</c>)
/// gets the frontend's own copy, through the platform's source replacement and offline, so the
/// build holds ONE such crate; and a frontend without such a crate builds and locks exactly as
/// before. The installed cargo really resolves and builds tiny crates; the materialized shared
/// crate is replaced by stand-ins of the same names (the real one needs the platform's crates,
/// which a test's vendor folder does not hold). Skipped on a machine without cargo.
/// </summary>
public class SharedCratePatchTests
{
    private static readonly Bundles.Crate Keep = new Bundles.Crate("keep", "2.0.0");

    private static async Task<string> CargoMustSucceed(string directory, params string[] arguments)
    {
        var run = await VendoredCrates.CargoAsync(directory, TestContext.Current.CancellationToken, arguments);
        run.Succeeded.Should().BeTrue("`cargo " + string.Join(" ", arguments) + "` should succeed, but said: " + run.Output);
        return run.Output;
    }

    /// <summary>Materializes a frontend, then puts stand-ins of the shared crate and its types crate where they were written.</summary>
    private static async Task<PlatformFrontendOptions> MaterializeWithStandInsAsync(TempFolder folder, string frontendDependencies, string frontendSource)
    {
        VendoredCrates.WritePlatformArchive(folder, Keep);
        folder.Write("frontend/Cargo.toml", "[package]\nname = \"named-frontend\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n"
            + $"{SharedRustCrate.CrateName} = {{ path = \"platform/{SharedRustCrate.CrateName}\" }}\n" + frontendDependencies);
        folder.Write("frontend/src/lib.rs", frontendSource);
        var options = new PlatformFrontendOptions { FrontendDirectory = folder.Combine("frontend"), BackendAssemblyPath = Frontends.SampleBackendAssembly };
        var frontend = new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = Array.Empty<CodeBrix.WebPlatform.Core.Crates.CrateBundle>() };
        await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        var shared = options.SharedCrateDirectory;
        Directory.Delete(shared, recursive: true);
        folder.Write($"frontend/platform/{SharedRustCrate.CrateName}/Cargo.toml",
            $"[package]\nname = \"{SharedRustCrate.CrateName}\"\nversion = \"1.0.0\"\nedition = \"2021\"\n\n[dependencies]\n"
            + $"{SharedRustCrate.TypesCrateName} = {{ path = \"{SharedRustCrate.TypesFolderName}\" }}\nkeep = \"2\"\n");
        folder.Write($"frontend/platform/{SharedRustCrate.CrateName}/src/lib.rs",
            $"pub use {SharedRustCrate.TypesRustName}::dates;\npub struct Marker;\npub fn keep_version() -> &'static str {{ keep::version() }}\n");
        folder.Write($"frontend/platform/{SharedRustCrate.CrateName}/{SharedRustCrate.TypesFolderName}/Cargo.toml",
            $"[package]\nname = \"{SharedRustCrate.TypesCrateName}\"\nversion = \"1.0.0\"\nedition = \"2021\"\n");
        folder.Write($"frontend/platform/{SharedRustCrate.CrateName}/{SharedRustCrate.TypesFolderName}/src/lib.rs", "pub mod dates { pub struct Date; }\n");
        return options;
    }

    [Fact]
    public async Task a_path_crate_that_names_the_shared_crate_by_version_gets_the_frontends_own_copy_offline()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, "cargo is not installed on this machine.");

        //Arrange
        using var folder = new TempFolder();
        folder.Write("shared/Cargo.toml", "[package]\nname = \"shared\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n"
            + $"{SharedRustCrate.CrateName} = \"*\"\n{SharedRustCrate.TypesCrateName} = \"*\"\n");
        folder.Write("shared/src/lib.rs",
            "pub fn marker() -> codebrix_webplatform::Marker { codebrix_webplatform::Marker }\n"
            + "pub fn date() -> codebrix_webplatform_types::dates::Date { codebrix_webplatform_types::dates::Date }\n");
        // The frontend hands the shared crate's values to its own copy's types: one crate, one set of types.
        var options = await MaterializeWithStandInsAsync(folder, "shared = { path = \"../shared\" }\n",
            "pub fn marker() -> codebrix_webplatform::Marker { shared::marker() }\n"
            + "pub fn date() -> codebrix_webplatform::dates::Date { shared::date() }\n");

        //Act
        await CargoMustSucceed(options.FrontendDirectory, "build", "--offline");
        await CargoMustSucceed(options.FrontendDirectory, "build", "--offline", "--locked");

        //Assert
        var lockFile = File.ReadAllText(folder.Combine("frontend/Cargo.lock"));
        lockFile.Split('\n').Count(l => l == $"name = \"{SharedRustCrate.CrateName}\"").Should().Be(1);
        lockFile.Split('\n').Count(l => l == $"name = \"{SharedRustCrate.TypesCrateName}\"").Should().Be(1);
        lockFile.Should().Contain($"name = \"shared\"\nversion = \"0.1.0\"\ndependencies = [\n \"{SharedRustCrate.CrateName}\",\n \"{SharedRustCrate.TypesCrateName}\",\n]");
        lockFile.Should().NotContain("[[patch.unused]]");
    }

    [Fact]
    public async Task a_frontend_without_such_a_crate_builds_and_locks_as_without_the_patch()
    {
        Assert.SkipUnless(VendoredCrates.CargoIsInstalled, "cargo is not installed on this machine.");

        //Arrange
        using var folder = new TempFolder();
        var options = await MaterializeWithStandInsAsync(folder, string.Empty, "pub fn version() -> &'static str { codebrix_webplatform::keep_version() }\n");

        //Act
        var output = await CargoMustSucceed(options.FrontendDirectory, "build", "--offline");

        //Assert
        output.Should().NotContain("patch", "cargo says nothing about a patch it did not need");
        var lockFile = File.ReadAllText(folder.Combine("frontend/Cargo.lock"));
        lockFile.Should().NotContain("[[patch.unused]]");
        lockFile.Should().Contain($"name = \"{SharedRustCrate.CrateName}\"\nversion = \"1.0.0\"\ndependencies = [\n \"{SharedRustCrate.TypesCrateName}\",\n \"keep\",\n]");
        File.ReadAllText(options.CargoConfigurationFile).Should().Contain("[patch.crates-io]\n");
    }
}
