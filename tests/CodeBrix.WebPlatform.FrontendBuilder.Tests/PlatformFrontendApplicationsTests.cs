using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>Several frontend applications in one build, and registered crate bundles.</summary>
public class PlatformFrontendApplicationsTests
{
    // A made-up crate that the platform's real Cargo.lock does not name: the starting lock file
    // MaterializeAsync writes is the real one, and a made-up version of one of its crates would
    // make that file's entry stale on the next materialize.
    private static readonly Bundles.Crate Platform1 = new Bundles.Crate("fake-sys", "0.3.1");
    private static readonly Bundles.Crate Wgpu = new Bundles.Crate("wgpu", "30.0.2");

    private static string Frontend(TempFolder folder, string name)
    {
        folder.Write($"{name}/Cargo.toml", $"[package]\nname = \"{name}-frontend\"\nversion = \"0.1.0\"\nedition = \"2021\"\n");
        return folder.Combine(name);
    }

    private static PlatformFrontendOptions ThreeApplications(TempFolder folder)
    {
        var options = new PlatformFrontendOptions { BackendAssemblyPath = Frontends.SampleBackendAssembly };
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Frontend(folder, "home") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Frontend(folder, "sales"), WasmSplit = true });
        options.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = Frontend(folder, "admin"), WasmSplit = false });
        return options;
    }

    private static PlatformFrontend Create(PlatformFrontendOptions options, TempFolder folder, params CrateBundle[] bundles)
    {
        Bundles.WritePlatformArchive(folder, Platform1);
        return new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = bundles };
    }

    [Fact]
    public void the_builder_knows_its_applications_and_their_folders()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        var frontend = Create(ThreeApplications(folder), folder);

        //Assert
        frontend.Applications.Select(a => a.Name).Should().Equal("app", "sales", "admin");
        frontend.FrontendDirectory.Should().Be(folder.Combine("home"));
        frontend.GetFrontendDirectory(null).Should().Be(folder.Combine("home"));
        frontend.GetFrontendDirectory("app").Should().Be(folder.Combine("home"));
        frontend.GetFrontendDirectory("sales").Should().Be(folder.Combine("sales"));
        frontend.GetOutputDirectory("admin", FrontendConfiguration.Release)
            .Should().Be(Path.Combine(folder.Combine("admin"), "target", "dx", "admin-frontend", "release", "web", "public"));
    }

    [Fact]
    public void GetFrontendDirectory_lists_the_applications_when_the_name_is_unknown()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(ThreeApplications(folder), folder);

        //Act
        Action act = () => frontend.GetFrontendDirectory("billing");

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*no frontend application named 'billing'*(no name), 'sales', 'admin'*");
    }

    [Theory]
    [InlineData(FrontendConfiguration.Release, null, false, "build --platform web --release --debug-symbols false")]
    [InlineData(FrontendConfiguration.Release, "app", false, "build --platform web --release --debug-symbols false")]
    [InlineData(FrontendConfiguration.Release, "sales", false, "build --platform web --release --debug-symbols false --base-path sales")]
    [InlineData(FrontendConfiguration.Release, "sales", true, "build --platform web --release --debug-symbols false --base-path sales --wasm-split")]
    [InlineData(FrontendConfiguration.Release, "", true, "build --platform web --release --debug-symbols false --wasm-split")]
    [InlineData(FrontendConfiguration.Debug, "admin", false, "build --platform web --base-path admin")]
    [InlineData(FrontendConfiguration.Debug, null, true, "build --platform web --wasm-split")]
    public void GetDxBuildArguments_adds_the_base_path_of_a_named_application_and_the_splitting(FrontendConfiguration configuration, string name, bool split, string expected)
        => string.Join(" ", PlatformFrontend.GetDxBuildArguments(configuration, name, split)).Should().Be(expected);

    [Fact]
    public void GetDxBuildArguments_refuses_a_bad_application_name()
    {
        //Act
        Action act = () => PlatformFrontend.GetDxBuildArguments(FrontendConfiguration.Release, "Sales", false);

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*capital letters*");
    }

    [Fact]
    public async Task MaterializeAsync_writes_every_applications_frontend_and_one_shared_vendor_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = ThreeApplications(folder);
        var frontend = Create(options, folder);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.ApplicationName.Should().Be("app");
        first.Applications.Select(a => a.ApplicationName).Should().Equal("app", "sales", "admin");
        first.Applications[0].Should().BeSameAs(first);
        first.AllWereUpToDate.Should().BeFalse();
        second.AllWereUpToDate.Should().BeTrue();
        foreach (var name in new[] { "home", "sales", "admin" })
        {
            File.Exists(folder.Combine(name + "/src/generated/mod.rs")).Should().BeTrue(name);
            File.Exists(folder.Combine(name + "/platform/codebrix-webplatform/Cargo.toml")).Should().BeTrue(name);
            File.Exists(folder.Combine(name + "/Cargo.lock")).Should().BeTrue(name);
        }

        Directory.Exists(folder.Combine("home/platform/vendor/fake-sys-0.3.1")).Should().BeTrue();
        Directory.Exists(folder.Combine("sales/platform/vendor")).Should().BeFalse("the applications share the first one's vendor folder");
        File.ReadAllText(folder.Combine("home/.cargo/config.toml")).Should().Contain("directory = \"platform/vendor\"");
        File.ReadAllText(folder.Combine("sales/.cargo/config.toml")).Should().Contain("directory = \"../home/platform/vendor\"");
        File.ReadAllText(folder.Combine("home/src/generated/mod.rs")).Should().Be(File.ReadAllText(folder.Combine("admin/src/generated/mod.rs")));
    }

    [Fact]
    public async Task MaterializeAsync_refuses_an_application_whose_folder_is_not_a_frontend_before_writing_anything()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = ThreeApplications(folder);
        File.Delete(folder.Combine("admin/Cargo.toml"));
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<FrontendBuildException>()).WithMessage("*admin*is not a frontend folder*");
        Directory.Exists(folder.Combine("home/src/generated")).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_writes_the_crates_of_a_registered_bundle_beside_the_platforms()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var bundle = Bundles.Create("graphics", "My.Graphics", "1.0", Wgpu, Platform1);
        var frontend = Create(options, folder, bundle);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.VendorChanged.Should().BeTrue();
        second.VendorChanged.Should().BeFalse();
        second.WasUpToDate.Should().BeTrue();
        Directory.GetDirectories(options.VendorDirectory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal)
            .Should().Equal("fake-sys-0.3.1", "wgpu-30.0.2");
        var outcome = second.CrateBundles.Single();
        outcome.Name.Should().Be("graphics");
        outcome.AddedCrates.Should().Equal("wgpu 30.0.2");
        outcome.SharedCrates.Should().Equal("fake-sys 0.3.1");
    }

    [Fact]
    public async Task MaterializeAsync_rewrites_the_vendor_folder_when_the_registered_bundles_change()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        await Create(options, folder, Bundles.Create("graphics", "My.Graphics", "1.0", Wgpu)).MaterializeAsync(TestContext.Current.CancellationToken);

        //Act
        var without = await Create(options, folder).MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        without.VendorChanged.Should().BeTrue();
        without.CrateBundles.Should().BeEmpty();
        Directory.Exists(Path.Combine(options.VendorDirectory, "wgpu-30.0.2")).Should().BeFalse();
        Directory.Exists(Path.Combine(options.VendorDirectory, "fake-sys-0.3.1")).Should().BeTrue();
    }

    [Fact]
    public async Task MaterializeAsync_stops_and_writes_no_crates_when_a_bundle_breaks_the_strict_rule()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var bundle = Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("fake-sys", "0.3.2"));
        var frontend = Create(options, folder, bundle);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<FrontendBuildException>()).WithMessage("*fake-sys: the crate bundle 'graphics' of My.Graphics 1.0 carries version 0.3.2*carries version 0.3.1*");
        Directory.Exists(options.VendorDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_checks_the_bundles_and_unpacks_nothing_when_the_application_adds_crates()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CrateMode = CrateMode.ApplicationAddsCrates;
        var good = Create(options, folder, Bundles.Create("graphics", "My.Graphics", "1.0", Wgpu));
        var bad = Create(options, folder, Bundles.Create("graphics", "My.Graphics", "1.0", new Bundles.Crate("fake-sys", "0.3.2")));

        //Act
        var result = await good.MaterializeAsync(TestContext.Current.CancellationToken);
        Func<Task> act = () => bad.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.CrateBundles.Single().AddedCrates.Should().Equal("wgpu 30.0.2");
        Directory.Exists(options.VendorDirectory).Should().BeFalse();
        await act.Should().ThrowAsync<FrontendBuildException>();
    }

    [Fact]
    public async Task MaterializeAsync_uses_the_bundles_a_program_registered()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        Bundles.WritePlatformArchive(folder, Platform1);
        var frontend = new PlatformFrontend(options, folder.Combine("build-program"));
        var name = "registered-" + Guid.NewGuid().ToString("N");
        CrateBundles.Register(Bundles.Create(name, "My.Registered", "1.0", new Bundles.Crate(name, "1.0.0")));

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.CrateBundles.Should().Contain(b => b.Name == name);
        Directory.Exists(Path.Combine(options.VendorDirectory, name + "-1.0.0")).Should().BeTrue();
    }

    [Fact]
    public async Task ServeAsync_says_which_applications_there_are_when_it_cannot_tell_which_is_meant()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = new PlatformFrontendOptions { BackendAssemblyPath = Frontends.SampleBackendAssembly };
        options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Frontend(folder, "sales") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = Frontend(folder, "admin") });
        var frontend = Create(options, folder);

        //Act
        Func<Task> noName = () => frontend.ServeAsync(TestContext.Current.CancellationToken);
        Func<Task> unknown = () => frontend.ServeAsync("billing", TestContext.Current.CancellationToken);

        //Assert
        (await noName.Should().ThrowAsync<FrontendBuildException>()).WithMessage("Name the frontend application to run*'sales', 'admin'*");
        (await unknown.Should().ThrowAsync<FrontendBuildException>()).WithMessage("*no frontend application named 'billing'*");
    }
}
