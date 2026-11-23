using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;
using LinuxArm64Tools = CodeBrix.WebPlatform.FrontendBuilder.LinuxArm64.HostTools;
using LinuxX64Tools = CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.HostTools;
using MacOSArm64Tools = CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64.HostTools;
using MacOSX64Tools = CodeBrix.WebPlatform.FrontendBuilder.MacOSX64.HostTools;
using WindowsX64Tools = CodeBrix.WebPlatform.FrontendBuilder.WindowsX64.HostTools;

namespace CodeBrix.WebPlatform.FrontendBuilder.HostTools.Tests;

/// <summary>
/// The five HostTools classes, and - for the machine the tests run on - the packaged programs in
/// this project's output folder (frontend-tools/&lt;rid&gt;/), copied there by the project
/// references exactly as the packages' buildTransitive targets copy them for a consumer; and the
/// standalone Tailwind program each host-tools project brings through its Styling package
/// dependency (tailwind/&lt;rid&gt;/).
/// </summary>
public class HostToolsTests
{
    private sealed record Platform(string RuntimeIdentifier, string DxFileName, string WasmBindgenFileName, string DxVersion, string WasmBindgenVersion, Func<string, string> GetToolDirectory);

    private static readonly IReadOnlyList<Platform> Platforms = new[]
    {
        new Platform(LinuxX64Tools.RuntimeIdentifier, LinuxX64Tools.DxFileName, LinuxX64Tools.WasmBindgenFileName, LinuxX64Tools.DxVersion, LinuxX64Tools.WasmBindgenVersion, LinuxX64Tools.GetToolDirectory),
        new Platform(LinuxArm64Tools.RuntimeIdentifier, LinuxArm64Tools.DxFileName, LinuxArm64Tools.WasmBindgenFileName, LinuxArm64Tools.DxVersion, LinuxArm64Tools.WasmBindgenVersion, LinuxArm64Tools.GetToolDirectory),
        new Platform(WindowsX64Tools.RuntimeIdentifier, WindowsX64Tools.DxFileName, WindowsX64Tools.WasmBindgenFileName, WindowsX64Tools.DxVersion, WindowsX64Tools.WasmBindgenVersion, WindowsX64Tools.GetToolDirectory),
        new Platform(MacOSArm64Tools.RuntimeIdentifier, MacOSArm64Tools.DxFileName, MacOSArm64Tools.WasmBindgenFileName, MacOSArm64Tools.DxVersion, MacOSArm64Tools.WasmBindgenVersion, MacOSArm64Tools.GetToolDirectory),
        new Platform(MacOSX64Tools.RuntimeIdentifier, MacOSX64Tools.DxFileName, MacOSX64Tools.WasmBindgenFileName, MacOSX64Tools.DxVersion, MacOSX64Tools.WasmBindgenVersion, MacOSX64Tools.GetToolDirectory),
    };

    private static readonly string[] ShippedBesideThePrograms =
    {
        "LICENSE-Dioxus.txt", "LICENSE-WasmBindgen.txt", "THIRD-PARTY-NOTICES-Dioxus.txt", "THIRD-PARTY-NOTICES-WasmBindgen.txt",
    };

    private static string CurrentRuntimeIdentifier()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        };
        if (OperatingSystem.IsWindows())
        {
            return "win-" + arch;
        }

        return (OperatingSystem.IsMacOS() ? "osx-" : "linux-") + arch;
    }

    private static Platform CurrentPlatform()
    {
        var rid = CurrentRuntimeIdentifier();
        var platform = Platforms.FirstOrDefault(p => p.RuntimeIdentifier == rid);
        Assert.SkipWhen(platform == null, $"No CodeBrix.WebPlatform.FrontendBuilder host-tools package is made for {rid}.");
        return platform;
    }

    private static string RepositoryFolderOf(string rid)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "native_libraries")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test project sits inside the repository, below native_libraries/");
        return Path.Combine(directory.FullName, "native_libraries", rid);
    }

    private static void SkipUnlessBuilt(Platform platform)
    {
        var source = RepositoryFolderOf(platform.RuntimeIdentifier);
        Assert.SkipUnless(
            File.Exists(Path.Combine(source, platform.DxFileName)) && File.Exists(Path.Combine(source, platform.WasmBindgenFileName)),
            $"The {platform.RuntimeIdentifier} programs are not built yet (native_libraries/{platform.RuntimeIdentifier}/ has none); see tools/dx-native-tools/README.txt.");
    }

    private static string FirstLineOf(string program, string arguments)
    {
        var start = new ProcessStartInfo(program, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(start);
        process.Should().NotBeNull();
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(60_000).Should().BeTrue($"{program} {arguments} should finish within a minute");
        process.ExitCode.Should().Be(0, $"{program} {arguments} should succeed");
        return output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
    }

    [Fact]
    public void RuntimeIdentifier_names_the_five_supported_platforms()
    {
        //Act
        var identifiers = Platforms.Select(p => p.RuntimeIdentifier);

        //Assert
        identifiers.Should().Equal("linux-x64", "linux-arm64", "win-x64", "osx-arm64", "osx-x64");
    }

    [Fact]
    public void file_names_carry_the_exe_suffix_on_windows_only()
    {
        //Assert
        foreach (var platform in Platforms)
        {
            var suffix = platform.RuntimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            platform.DxFileName.Should().Be("dx" + suffix);
            platform.WasmBindgenFileName.Should().Be("wasm-bindgen" + suffix);
            platform.DxVersion.Should().Be("0.7.10");
            platform.WasmBindgenVersion.Should().Be("0.2.130");
        }
    }

    [Fact]
    public void GetToolDirectory_is_frontend_tools_and_the_runtime_identifier_below_the_base_folder()
    {
        //Arrange
        var baseFolder = Path.Combine(Path.GetTempPath(), "app");

        //Assert
        foreach (var platform in Platforms)
        {
            platform.GetToolDirectory(baseFolder).Should().Be(Path.Combine(baseFolder, "frontend-tools", platform.RuntimeIdentifier));
        }
    }

    [Fact]
    public void GetToolDirectory_rejects_a_blank_base_folder()
    {
        //Arrange
        Action act = () => LinuxX64Tools.GetToolDirectory(" ");

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void the_current_platforms_programs_and_their_licence_and_notices_files_are_in_the_output()
    {
        //Arrange
        var platform = CurrentPlatform();
        SkipUnlessBuilt(platform);

        //Act
        var folder = platform.GetToolDirectory(AppContext.BaseDirectory);

        //Assert
        File.Exists(Path.Combine(folder, platform.DxFileName)).Should().BeTrue();
        File.Exists(Path.Combine(folder, platform.WasmBindgenFileName)).Should().BeTrue();
        foreach (var name in ShippedBesideThePrograms)
        {
            File.Exists(Path.Combine(folder, name)).Should().BeTrue($"{name} travels with the programs");
        }

        Directory.EnumerateFiles(folder, "*.provenance.txt").Should().BeEmpty("the build records stay in the repository");
    }

    [Fact]
    public void the_current_platforms_notices_files_are_the_generated_ones()
    {
        //Arrange
        var platform = CurrentPlatform();
        SkipUnlessBuilt(platform);
        var folder = platform.GetToolDirectory(AppContext.BaseDirectory);
        var generated = Path.Combine(Path.GetDirectoryName(RepositoryFolderOf(platform.RuntimeIdentifier)), "..", "tools", "dx-native-tools", "notices");

        //Assert
        foreach (var name in new[] { "THIRD-PARTY-NOTICES-Dioxus.txt", "THIRD-PARTY-NOTICES-WasmBindgen.txt" })
        {
            File.ReadAllText(Path.Combine(folder, name)).Should().Be(
                File.ReadAllText(Path.Combine(generated, name)),
                $"native_libraries/{platform.RuntimeIdentifier}/{name} must be a copy of tools/dx-native-tools/notices/{name} (LibraryChecker notices --write)");
        }
    }

    [Fact]
    public async Task the_current_platforms_programs_report_the_expected_versions()
    {
        //Arrange
        var platform = CurrentPlatform();
        SkipUnlessBuilt(platform);
        var folder = platform.GetToolDirectory(AppContext.BaseDirectory);
        var wasmBindgen = Path.Combine(folder, platform.WasmBindgenFileName);
        var frontend = Path.Combine(Path.GetTempPath(), "hosttools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(frontend);
        PrerequisiteReport report;
        try
        {
            // dx does not start without libgit2: the library starts it with the package's copy.
            report = await new PlatformFrontend(new PlatformFrontendOptions { FrontendDirectory = frontend }).CheckPrerequisitesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(frontend, recursive: true);
        }

        //Act
        var wasmBindgenVersion = FirstLineOf(wasmBindgen, "--version");

        //Assert
        report[PrerequisiteKind.HostTools].Status.Should().Be(PrerequisiteStatus.Ok, report[PrerequisiteKind.HostTools].Message);
        report[PrerequisiteKind.Libgit2].Status.Should().Be(PrerequisiteStatus.Ok, report[PrerequisiteKind.Libgit2].Message);
        report.DxVersion.Should().Be($"dioxus {platform.DxVersion} (57d6794)");
        wasmBindgenVersion.Should().Be($"wasm-bindgen {platform.WasmBindgenVersion}");
    }

    [Fact]
    public void every_platforms_tailwind_comes_with_its_host_tools_project()
    {
        //Assert
        foreach (var platform in Platforms)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, PackagedTailwind.FolderName, platform.RuntimeIdentifier);
            var suffix = platform.RuntimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            File.Exists(Path.Combine(folder, PackagedTailwind.ProgramName + suffix)).Should().BeTrue($"the {platform.RuntimeIdentifier} host-tools project depends on {PackagedTailwind.GetPackageId(platform.RuntimeIdentifier)}");
            File.Exists(Path.Combine(folder, "LICENSE-Tailwind.txt")).Should().BeTrue();
            File.Exists(Path.Combine(folder, "THIRD-PARTY-NOTICES-Tailwind.txt")).Should().BeTrue();
            File.ReadAllText(Path.Combine(folder, "version.txt")).Trim().Should().Be("4.3.5");
        }
    }

    [Fact]
    public async Task the_current_platforms_tailwind_is_the_one_a_build_runs()
    {
        //Arrange
        var platform = CurrentPlatform();
        var frontend = Path.Combine(Path.GetTempPath(), "hosttools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(frontend);
        PrerequisiteReport report;
        try
        {
            //Act
            report = await new PlatformFrontend(new PlatformFrontendOptions { FrontendDirectory = frontend }).CheckPrerequisitesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(frontend, recursive: true);
        }

        //Assert
        report[PrerequisiteKind.Tailwind].Status.Should().Be(PrerequisiteStatus.Ok, report[PrerequisiteKind.Tailwind].Message);
        report[PrerequisiteKind.Tailwind].Message.Should().Contain(PackagedTailwind.GetPackageId(platform.RuntimeIdentifier));
        report.Tailwind.Directory.Should().Be(Path.Combine(AppContext.BaseDirectory, PackagedTailwind.FolderName, platform.RuntimeIdentifier));
        report.TailwindVersion.Should().Be("4.3.5");
    }
}
