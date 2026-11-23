using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.BackendMapper;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using CodeBrix.WebPlatform.WasmShrink;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

public class PlatformFrontendTests
{
    private static PlatformFrontend Create(PlatformFrontendOptions options, TempFolder folder)
    {
        if (!File.Exists(PlatformCrates.GetArchivePath(folder.Combine("build-program"))))
        {
            Frontends.WriteCratesArchive(folder);
        }

        // No bundles: what another test registers for the whole process must not show up here.
        return new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = Array.Empty<CodeBrix.WebPlatform.Core.Crates.CrateBundle>() };
    }

    private static HostTools FakeTools(TempFolder folder)
    {
        folder.Write("build-program/frontend-tools/linux-x64/dx", "dx");
        folder.Write("build-program/frontend-tools/linux-x64/wasm-bindgen", "wasm-bindgen");
        folder.Write("build-program/tailwind/linux-x64/tailwindcss", "tailwindcss");
        HostToolsLocator.TryLocate(folder.Combine("build-program"), "linux-x64", null, null, out var tools);
        return tools;
    }

    [Fact]
    public void the_constructor_refuses_missing_options()
    {
        //Act
        Action noOptions = () => new PlatformFrontend(null);
        Action noFolder = () => new PlatformFrontend(new PlatformFrontendOptions { FrontendDirectory = " " });

        //Assert
        noOptions.Should().Throw<ArgumentNullException>();
        noFolder.Should().Throw<ArgumentException>().WithMessage("*FrontendDirectory*");
    }

    [Fact]
    public void the_options_put_the_build_output_in_fixed_places_inside_the_frontend()
    {
        //Arrange
        var frontend = Path.Combine(Path.GetTempPath(), "app", "frontend");
        var options = new PlatformFrontendOptions { FrontendDirectory = frontend };

        //Assert
        options.GeneratedClientDirectory.Should().Be(Path.Combine(frontend, "src", "generated"));
        options.SharedCrateDirectory.Should().Be(Path.Combine(frontend, "platform", "codebrix-webplatform"));
        options.ResolvedVendorDirectory.Should().Be(Path.Combine(frontend, "platform", "vendor"));
        options.CargoConfigurationFile.Should().Be(Path.Combine(frontend, ".cargo", "config.toml"));
        options.CrateMode.Should().Be(CrateMode.PlatformCratesOnly);
        new PlatformFrontendOptions().GeneratedClientDirectory.Should().BeNull();
        new PlatformFrontendOptions().ResolvedVendorDirectory.Should().BeNull();
        options.Shrink.Should().BeNull();
    }

    [Fact]
    public void GetDxBuildArguments_makes_a_release_build_without_debug_symbols()
        => PlatformFrontend.GetDxBuildArguments(FrontendConfiguration.Release).Should().Equal("build", "--platform", "web", "--release", "--debug-symbols", "false");

    [Fact]
    public void GetDxBuildArguments_makes_a_plain_debug_build()
        => PlatformFrontend.GetDxBuildArguments(FrontendConfiguration.Debug).Should().Equal("build", "--platform", "web");

    [Theory]
    [InlineData("  1.2s ERROR wasm-opt failed with status code exit status: 1\nstderr: unknown option", true)]
    [InlineData("  1.9s  INFO Client build completed successfully!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ReportsShrinkFailure_finds_the_failure_dx_only_logs(string output, bool expected)
        => PlatformFrontend.ReportsShrinkFailure(output).Should().Be(expected);

    [Fact]
    public void CreateDxEnvironment_puts_wasm_bindgen_on_PATH_ahead_of_the_machines_own()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var tools = FakeTools(folder);

        //Act
        var environment = frontend.CreateDxEnvironment(tools);

        //Assert
        environment["PATH"].Should().Contain(tools.WasmBindgenDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
    }

    [Fact]
    public void CreateDxEnvironment_puts_the_packaged_tailwind_first_on_PATH_and_then_the_packaged_wasm_opt()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var tools = FakeTools(folder);
        var tailwind = folder.Combine("build-program/tailwind/linux-x64");
        var packaged = WasmShrinkTool.IsAvailable ? WasmShrinkTool.GetToolDirectory() : null;

        //Act
        var path = frontend.CreateDxEnvironment(tools)["PATH"];

        //Assert
        path.Should().StartWith(tailwind + Path.PathSeparator + (packaged ?? tools.WasmBindgenDirectory) + Path.PathSeparator);
    }

    [Fact]
    public void CreateDxEnvironment_refuses_without_the_packaged_tailwind()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var tools = FakeTools(folder);
        File.Delete(folder.Combine("build-program/tailwind/linux-x64/tailwindcss"));

        //Act
        Action act = () => frontend.CreateDxEnvironment(tools);

        //Assert
        act.Should().Throw<FrontendBuildException>()
            .WithMessage("*packaged Tailwind program was not found*CodeBrix.WebPlatform.Styling.Tailwind4.LinuxX64.MitLicenseForever*CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever*");
    }

    [Fact]
    public void CreateDxEnvironment_keeps_dx_out_of_the_users_own_dx_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var byDefault = Create(options, folder).CreateDxEnvironment(FakeTools(folder))["DX_HOME"];
        options.DxHomeDirectory = folder.Combine("artifacts/dx-home");

        //Act
        var named = Create(options, folder).CreateDxEnvironment(FakeTools(folder))["DX_HOME"];

        //Assert
        byDefault.Should().Be(Path.Combine(options.FrontendDirectory, "target", "dx-home"));
        named.Should().Be(folder.Combine("artifacts/dx-home"));
    }

    [Fact]
    public void the_packaged_wasm_opt_is_beside_the_build_program_when_the_package_has_this_platform()
    {
        //Arrange
        Assert.SkipWhen(!WasmShrinkTool.IsSupportedPlatform, "The CodeBrix.WebPlatform.WasmShrink package does not include wasm-opt for this platform yet.");

        //Assert
        File.Exists(WasmShrinkTool.GetExecutablePath()).Should().BeTrue();
    }

    [Fact]
    public void GetOutputDirectory_is_dx_output_folder_for_the_frontends_package()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);

        //Act
        var release = frontend.GetOutputDirectory(FrontendConfiguration.Release);
        var debug = frontend.GetOutputDirectory(FrontendConfiguration.Debug);

        //Assert
        release.Should().Be(Path.Combine(options.FrontendDirectory, "target", "dx", "sample-frontend", "release", "web", "public"));
        debug.Should().Be(Path.Combine(options.FrontendDirectory, "target", "dx", "sample-frontend", "debug", "web", "public"));
    }

    [Fact]
    public void GetOutputDirectory_refuses_a_folder_that_is_not_a_frontend()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(new PlatformFrontendOptions { FrontendDirectory = folder.Path }, folder);

        //Act
        Action act = () => frontend.GetOutputDirectory(FrontendConfiguration.Release);

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*is not a frontend folder*Cargo.toml*");
    }

    [Fact]
    public async Task BuildAsync_accepts_an_unshrunk_release_build_and_goes_on_to_look_for_dx()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.Shrink = false;
        var frontend = Create(options, folder);
        var package = HostTools.GetPackageId(HostTools.CurrentRuntimeIdentifier);

        //Act
        Func<Task> act = () => frontend.BuildAsync(FrontendConfiguration.Release, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage(package == null ? "*not available for this machine*" : $"*{package}*");
    }

    [Fact]
    public void CreateDxEnvironment_supplies_nothing_more_to_a_dx_that_does_not_link_libgit2()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var tools = FakeTools(folder);

        //Act
        var environment = frontend.CreateDxEnvironment(tools);

        //Assert
        environment.Keys.Should().BeEquivalentTo(new[] { "PATH", "DX_HOME" });
        Directory.Exists(frontend.Libgit2Directory).Should().BeFalse();
    }

    [Fact]
    public void Libgit2Directory_is_inside_dx_home()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);

        //Assert
        frontend.Libgit2Directory.Should().Be(Path.Combine(frontend.DxHomeDirectory, "libgit2"));
    }

    [Fact]
    public async Task BuildAsync_names_the_host_tools_package_when_dx_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var package = HostTools.GetPackageId(HostTools.CurrentRuntimeIdentifier);

        //Act
        Func<Task> act = () => frontend.BuildAsync(cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage(package == null ? "*not available for this machine*" : $"*{package}*");
    }

    [Fact]
    public async Task ServeAsync_names_the_host_tools_package_when_dx_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);

        //Act
        Func<Task> act = () => frontend.ServeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*dx and wasm-bindgen*");
    }

    [Fact]
    public async Task BuildAsync_refuses_before_starting_anything_when_the_packaged_tailwind_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);
        var package = PackagedTailwind.GetPackageId(HostTools.CurrentRuntimeIdentifier);

        //Act
        Func<Task> act = () => frontend.BuildAsync(cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        var message = (await act.Should().ThrowAsync<FrontendBuildException>()).Which.Message;
        message.Should().StartWith("The build cannot start: this machine is missing what it needs.");
        message.Should().Contain(package == null ? "The Tailwind program is not available for this machine" : "The packaged Tailwind program was not found at");
        if (package != null)
        {
            message.Should().Contain(package);
        }

        Directory.Exists(Path.Combine(options.FrontendDirectory, "target", "dx")).Should().BeFalse();
    }

    [Fact]
    public async Task ServeAsync_refuses_before_starting_dx_when_the_packaged_tailwind_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.ServeAsync(TestContext.Current.CancellationToken);

        //Assert
        var message = (await act.Should().ThrowAsync<FrontendBuildException>()).Which.Message;
        message.Should().StartWith("dx serve cannot start: this machine is missing what it needs.");
        message.Should().Contain("Tailwind program");
    }

    [Fact]
    public void FindHostTools_takes_the_package_folder_beside_the_build_program()
    {
        //Arrange
        Assert.SkipWhen(HostTools.GetPackageId(HostTools.CurrentRuntimeIdentifier) == null, "This machine is not a build platform.");
        using var folder = new TempFolder();
        var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        folder.Write($"build-program/frontend-tools/{HostTools.CurrentRuntimeIdentifier}/dx{suffix}", "dx");
        folder.Write($"build-program/frontend-tools/{HostTools.CurrentRuntimeIdentifier}/wasm-bindgen{suffix}", "wasm-bindgen");
        var frontend = Create(Frontends.CreateOptions(folder), folder);

        //Act
        var tools = frontend.FindHostTools();

        //Assert
        tools.Source.Should().Be(HostToolsSource.Package);
    }

    [Fact]
    public async Task CheckPrerequisitesAsync_reports_one_item_per_kind_and_the_missing_host_tools()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);

        //Act
        var report = await frontend.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        //Assert
        report.Items.Select(i => i.Kind).Should().Equal(PrerequisiteKind.Rust, PrerequisiteKind.Tailwind, PrerequisiteKind.WasmOpt, PrerequisiteKind.HostTools, PrerequisiteKind.Libgit2);
        report[PrerequisiteKind.HostTools].Status.Should().Be(PrerequisiteStatus.Missing);
        report[PrerequisiteKind.Tailwind].Status.Should().Be(PrerequisiteStatus.Missing);
        report.Tailwind.Should().BeNull();
        report.TailwindVersion.Should().BeNull();
        report[PrerequisiteKind.Libgit2].Status.Should().Be(PrerequisiteStatus.Note);
        report[PrerequisiteKind.Libgit2].Message.Should().Contain("not checked");
        report.HostTools.Should().BeNull();
        report.DxVersion.Should().BeNull();
        report.IsReady.Should().BeFalse();
        report.Items.Should().OnlyContain(i => i.Message.Length > 0);
    }

    [Fact]
    public async Task CheckPrerequisitesAsync_finds_the_tools_named_in_the_options()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.DxPath = folder.Write("own/dx", "dx");
        options.WasmBindgenPath = folder.Write("own/wasm-bindgen", "wasm-bindgen");
        var frontend = Create(options, folder);

        //Act
        var report = await frontend.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        //Assert
        report[PrerequisiteKind.HostTools].Status.Should().Be(PrerequisiteStatus.Ok);
        report.HostTools.Source.Should().Be(HostToolsSource.Options);
    }

    [Fact]
    public async Task MaterializeAsync_writes_the_client_code_and_the_shared_crate_once()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.Map.AssemblyName.Should().Be("CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend");
        first.GeneratedFiles.Should().Contain("mod.rs");
        first.SharedCrateFiles.Should().Equal(SharedRustCrate.Files.Select(f => f.RelativePath));
        first.WasUpToDate.Should().BeFalse();
        second.WasUpToDate.Should().BeTrue();
        File.ReadAllText(Path.Combine(options.GeneratedClientDirectory, "mod.rs")).Should().StartWith(RustClientGenerator.GeneratedMarker);
        File.Exists(Path.Combine(options.SharedCrateDirectory, "Cargo.toml")).Should().BeTrue();
    }

    [Fact]
    public async Task MaterializeAsync_writes_with_the_applications_client_options()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.ClientOptions = new RustClientOptions { SignalRModulePath = "crate::signalr" };
        var frontend = Create(options, folder);

        //Act
        await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        File.ReadAllText(Path.Combine(options.GeneratedClientDirectory, "hubs.rs")).Should().Contain("use crate::signalr::{self as signalr,");
    }

    [Fact]
    public async Task MaterializeAsync_says_what_is_wrong_when_the_backend_has_not_been_built()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.BackendAssemblyPath = folder.Combine("backend/bin/Release/net10.0/Missing.Api.dll");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        var thrown = await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("The client code could not be generated from the backend.*does not exist*");
        thrown.Which.InnerException.Should().BeOfType<BackendMapException>();
    }

    [Fact]
    public async Task MaterializeAsync_wants_the_backend_named()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(new PlatformFrontendOptions { FrontendDirectory = Frontends.CreateFrontend(folder) }, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*BackendProjectPath*BackendAssemblyPath*");
    }

    [Fact]
    public async Task MaterializeAsync_refuses_a_backend_project_that_does_not_exist()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.BackendProjectPath = folder.Combine("backend/Missing.Api.csproj");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*Missing.Api.csproj' does not exist*");
    }

    [Fact]
    public async Task MaterializeAsync_refuses_to_write_over_hand_written_code()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        folder.Write("frontend/src/generated/mine.rs", "// written by hand");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*'mine.rs', which was not generated*");
    }

    [Fact]
    public async Task MaterializeAsync_refuses_a_shared_crate_folder_that_holds_something_else()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        folder.Write("frontend/platform/codebrix-webplatform/notes.txt", "mine");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*Nothing was written*");
    }

    [Fact]
    public async Task MaterializeAsync_writes_the_platforms_crates_the_cargo_configuration_and_a_starting_lock_file()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.VendorChanged.Should().BeTrue();
        first.CargoConfigurationWritten.Should().BeTrue();
        first.LockFileWritten.Should().BeTrue();
        second.WasUpToDate.Should().BeTrue();
        File.Exists(Path.Combine(options.ResolvedVendorDirectory, "fake-crate-1.0.0", "Cargo.toml")).Should().BeTrue();
        File.Exists(Path.Combine(options.ResolvedVendorDirectory, PlatformCrates.StampFileName)).Should().BeTrue();
        var configuration = File.ReadAllText(options.CargoConfigurationFile);
        configuration.Should().Contain("replace-with = \"codebrix-webplatform-crates\"");
        configuration.Should().Contain("directory = \"platform/vendor\"");
        configuration.Should().Contain("offline = true");
        File.ReadAllText(folder.Combine("frontend/Cargo.lock")).Should().Be(LockFileRules.AddMarker(PlatformCrates.LockFileText));
    }

    [Fact]
    public async Task MaterializeAsync_unpacks_the_crates_again_only_when_the_archive_changed()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);
        await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        Frontends.WriteCratesArchive(folder, "other-crate-2.0.0");

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.VendorChanged.Should().BeTrue();
        result.CargoConfigurationWritten.Should().BeFalse();
        Directory.EnumerateDirectories(options.ResolvedVendorDirectory).Select(Path.GetFileName).Should().Equal("other-crate-2.0.0");
    }

    [Fact]
    public async Task MaterializeAsync_lets_an_application_add_crates_from_crates_io()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);
        await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        options.CrateMode = CrateMode.ApplicationAddsCrates;

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.VendorChanged.Should().BeTrue("the vendor folder the platform wrote is removed");
        Directory.Exists(options.ResolvedVendorDirectory).Should().BeFalse();
        var configuration = File.ReadAllText(options.CargoConfigurationFile);
        configuration.Should().Contain("CrateMode.ApplicationAddsCrates");
        configuration.Should().NotContain("[source.");
        configuration.Should().NotContain("offline");
    }

    [Fact]
    public async Task MaterializeAsync_keeps_the_applications_own_lock_file()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var lockFile = folder.Write("frontend/Cargo.lock", "# the application's own\nversion = 4\n");
        var frontend = Create(options, folder);

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileWritten.Should().BeFalse();
        File.ReadAllText(lockFile).Should().Be("# the application's own\nversion = 4\n");
    }

    [Fact]
    public async Task MaterializeAsync_refuses_a_vendor_folder_it_did_not_write()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.VendorDirectory = folder.Combine("my-crates");
        folder.Write("my-crates/mine/Cargo.toml", "[package]\nname = \"mine\"\n");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*vendor folder*did not write*");
        File.Exists(folder.Combine("my-crates/mine/Cargo.toml")).Should().BeTrue();
    }

    [Fact]
    public async Task MaterializeAsync_refuses_a_cargo_configuration_it_did_not_write()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        folder.Write("frontend/.cargo/config.toml", "[build]\ntarget = \"wasm32-unknown-unknown\"\n");
        var frontend = Create(options, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*config.toml' was not written by CodeBrix.WebPlatform.FrontendBuilder*");
    }

    [Fact]
    public async Task MaterializeAsync_names_the_package_when_the_crates_are_not_beside_the_build_program()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);
        File.Delete(PlatformCrates.GetArchivePath(folder.Combine("build-program")));

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*platform-crates.zip' does not exist*CodeBrix.WebPlatform.FrontendBuilder package*");
    }

    [Fact]
    public async Task MaterializeAsync_refuses_a_folder_without_a_cargo_manifest()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(new PlatformFrontendOptions { FrontendDirectory = folder.Path, BackendAssemblyPath = Frontends.SampleBackendAssembly }, folder);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<FrontendBuildException>().WithMessage("*is not a frontend folder*");
    }

    [Fact]
    public async Task ResolveTargetPathAsync_asks_msbuild_where_the_release_build_puts_the_assembly()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = Create(Frontends.CreateOptions(folder), folder);
        var project = Frontends.SampleBackendProject;

        //Act
        var path = await frontend.ResolveTargetPathAsync(project, TestContext.Current.CancellationToken);

        //Assert
        path.Should().Be(Path.Combine(Path.GetDirectoryName(project), "bin", "Release", "net10.0", "CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend.dll"));
    }

    private const string TailwindInput = "@import \"tailwindcss\";\n";

    [Fact]
    public void the_options_name_the_stylesheets_where_dx_expects_them()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);

        //Act
        var input = options.ResolvedTailwindInputFile;
        var output = options.ResolvedTailwindOutputFile;
        options.TailwindInputFile = "styles/input.css";
        options.TailwindOutputFile = folder.Combine("elsewhere/out.css");

        //Assert
        options.GenerateMissingStylesheet.Should().BeTrue();
        input.Should().Be(Path.Combine(options.FrontendDirectory, "tailwind.css"));
        output.Should().Be(Path.Combine(options.FrontendDirectory, "assets", "tailwind.css"));
        options.ResolvedTailwindInputFile.Should().Be(Path.Combine(options.FrontendDirectory, "styles", "input.css"));
        options.ResolvedTailwindOutputFile.Should().Be(folder.Combine("elsewhere/out.css"));
    }

    [Fact]
    public async Task MaterializeAsync_reports_no_stylesheet_for_a_frontend_without_tailwind()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(options, folder);

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.Stylesheet.Should().Be(StylesheetOutcome.NotApplicable);
        result.StylesheetMissing.Should().BeFalse();
        File.Exists(options.ResolvedTailwindOutputFile).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureStylesheetAsync_leaves_an_existing_stylesheet_alone()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        File.WriteAllText(options.ResolvedTailwindInputFile, TailwindInput);
        folder.Write("frontend/assets/tailwind.css", "/* built by dx */");
        var frontend = Create(options, folder);

        //Act
        var (outcome, _) = await frontend.EnsureStylesheetAsync(TestContext.Current.CancellationToken);

        //Assert
        outcome.Should().Be(StylesheetOutcome.AlreadyPresent);
        File.ReadAllText(options.ResolvedTailwindOutputFile).Should().Be("/* built by dx */");
    }

    [Fact]
    public async Task EnsureStylesheetAsync_does_nothing_when_switched_off()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.GenerateMissingStylesheet = false;
        File.WriteAllText(options.ResolvedTailwindInputFile, TailwindInput);
        var frontend = Create(options, folder);

        //Act
        var (outcome, message) = await frontend.EnsureStylesheetAsync(TestContext.Current.CancellationToken);

        //Assert
        outcome.Should().Be(StylesheetOutcome.SwitchedOff);
        message.Should().Contain("GenerateMissingStylesheet");
        File.Exists(options.ResolvedTailwindOutputFile).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_reports_but_does_not_fail_when_tailwind_is_missing()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        File.WriteAllText(options.ResolvedTailwindInputFile, TailwindInput);
        var output = new StringWriter();
        options.Output = output;
        var frontend = Create(options, folder);

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.Stylesheet.Should().Be(StylesheetOutcome.SkippedTailwindNotAvailable);
        result.StylesheetMissing.Should().BeTrue();
        result.StylesheetMessage.Should().Contain("was not generated because").And.Contain("Tailwind program").And.Contain("Plain cargo commands");
        output.ToString().Should().Contain(result.StylesheetMessage);
        result.GeneratedFiles.Should().Contain("mod.rs");
        File.Exists(options.ResolvedTailwindOutputFile).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureStylesheetAsync_reports_a_packaged_tailwind_that_fails()
    {
        //Arrange
        Assert.SkipWhen(PackagedTailwind.GetPackageId(HostTools.CurrentRuntimeIdentifier) == null, "This machine is not a build platform.");
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        File.WriteAllText(options.ResolvedTailwindInputFile, TailwindInput);
        var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        folder.Write($"build-program/tailwind/{HostTools.CurrentRuntimeIdentifier}/tailwindcss{suffix}", "not a program");
        var frontend = Create(options, folder);

        //Act
        var (outcome, message) = await frontend.EnsureStylesheetAsync(TestContext.Current.CancellationToken);

        //Assert
        outcome.Should().Be(StylesheetOutcome.SkippedTailwindFailed);
        message.Should().Contain("the Tailwind program failed");
        File.Exists(options.ResolvedTailwindOutputFile).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_generates_a_missing_stylesheet_with_the_packaged_tailwind()
    {
        //Arrange
        var probe = await new PlatformFrontend(new PlatformFrontendOptions { FrontendDirectory = Path.GetTempPath() })
            .CheckPrerequisitesAsync(TestContext.Current.CancellationToken);
        Assert.SkipWhen(probe[PrerequisiteKind.Tailwind].Status != PrerequisiteStatus.Ok,
            "The packaged Tailwind program is not in this test project's output: " + probe[PrerequisiteKind.Tailwind].Message);
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        File.WriteAllText(options.ResolvedTailwindInputFile, TailwindInput + "@source \"./src/**/*.rs\";\n");
        folder.Write("frontend/src/main.rs", "fn main() { let _ = \"flex text-indigo-600\"; }\n");
        var copy = Directory.CreateDirectory(folder.Combine($"build-program/tailwind/{probe.Tailwind.RuntimeIdentifier}")).FullName;
        File.Copy(probe.Tailwind.Path, Path.Combine(copy, Path.GetFileName(probe.Tailwind.Path)));
        var frontend = Create(options, folder);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        probe.TailwindVersion.Should().Be(File.ReadAllText(Path.Combine(probe.Tailwind.Directory, "version.txt")).Trim());
        first.Stylesheet.Should().Be(StylesheetOutcome.Generated, first.StylesheetMessage);
        File.ReadAllText(options.ResolvedTailwindOutputFile).Should().Contain(".flex");
        second.Stylesheet.Should().Be(StylesheetOutcome.AlreadyPresent);
        second.WasUpToDate.Should().BeTrue();
    }
}
