using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.LibGit2;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Tools;

/// <summary>
/// Starts the dx the repository has adopted for this machine (native_libraries/&lt;rid&gt;/) through
/// the library, with the libgit2 of the CodeBrix.LibGit2 package that this test project gets
/// through the FrontendBuilder library (its native file is in this project's output, under
/// runtimes/&lt;rid&gt;/native/). Skipped on a machine with no adopted programs.
/// </summary>
public class Libgit2LaunchTests
{
    private static string AdoptedProgram(string name)
    {
        var rid = HostTools.CurrentRuntimeIdentifier;
        var suffix = rid != null && rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "native_libraries", rid ?? "none", name + suffix);
            if (Directory.Exists(Path.Combine(directory.FullName, "native_libraries")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static PlatformFrontend CreateWithAdoptedTools(TempFolder folder)
    {
        var dx = AdoptedProgram(HostTools.DxName);
        var wasmBindgen = AdoptedProgram(HostTools.WasmBindgenName);
        Assert.SkipUnless(dx != null && File.Exists(dx) && File.Exists(wasmBindgen), $"No dx is adopted for {HostTools.CurrentRuntimeIdentifier} in native_libraries/.");
        return new PlatformFrontend(new PlatformFrontendOptions
        {
            FrontendDirectory = Frontends.CreateFrontend(folder),
            DxPath = dx,
            WasmBindgenPath = wasmBindgen,
            DxHomeDirectory = folder.Combine("dx-home"),
        });
    }

    private static (int ExitCode, string Output) Run(string program, IEnumerable<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, IEnumerable<string> remove = null)
    {
        var start = new ProcessStartInfo(program)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var name in remove ?? Enumerable.Empty<string>())
        {
            start.Environment.Remove(name);
        }

        foreach (var pair in environment ?? new Dictionary<string, string>())
        {
            start.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(120_000).Should().BeTrue($"{program} should finish within two minutes");
        return (process.ExitCode, output.Result + error.Result);
    }

    [Fact]
    public async Task CheckPrerequisitesAsync_starts_the_adopted_dx_with_the_packaged_libgit2()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = CreateWithAdoptedTools(folder);

        //Act
        var report = await frontend.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        //Assert
        report[PrerequisiteKind.Libgit2].Status.Should().Be(PrerequisiteStatus.Ok, report[PrerequisiteKind.Libgit2].Message);
        report[PrerequisiteKind.Libgit2].Message.Should().Contain(Libgit2Supply.PackageId);
        report.DxVersion.Should().Be("dioxus 0.7.10 (57d6794)");
        Directory.EnumerateFiles(frontend.Libgit2Directory).Should().HaveCount(1, "the folder holds the library under the one name dx asks for");
    }

    [Fact]
    public void the_adopted_dx_needs_the_library_the_package_provides()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = CreateWithAdoptedTools(folder);
        var tools = frontend.FindHostTools();

        //Act
        var problem = frontend.PrepareLibgit2(tools, out var supply);

        //Assert
        problem.Should().BeNull();
        supply.IsNeeded.Should().BeTrue();
        Libgit2Naming.CommitOf(supply.NeededName).Should().Be(GlobalSettings.Version.LibGit2CommitSha);
        File.Exists(Path.Combine(frontend.Libgit2Directory, supply.LoaderFileName)).Should().BeTrue();
        new FileInfo(Path.Combine(frontend.Libgit2Directory, supply.LoaderFileName)).Length.Should().Be(new FileInfo(supply.SourceFile).Length);
    }

    [Fact]
    public void the_adopted_dx_does_not_start_without_the_library()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = CreateWithAdoptedTools(folder);
        var tools = frontend.FindHostTools();
        frontend.PrepareLibgit2(tools, out var supply);
        Assert.SkipWhen(supply.Naming.System == Libgit2System.Windows, "On Windows the loader also searches dx.exe's own folder and may show a dialog; not run here.");

        //Act
        var (exitCode, output) = Run(tools.DxPath, new[] { "--version" }, folder.Path, null, new[] { supply.Naming.LoaderVariable });

        //Assert
        exitCode.Should().NotBe(0);
        output.Should().Contain(supply.LoaderFileName);
    }

    [Fact]
    public void dx_new_creates_a_project_from_a_local_template_in_the_environment_the_library_prepares()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = CreateWithAdoptedTools(folder);
        var tools = frontend.FindHostTools();
        var template = folder.Combine("template");
        Repository.Init(template);
        folder.Write("template/Cargo.toml", "[package]\nname = \"{{project-name}}\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n");
        folder.Write("template/src/main.rs", "fn main() {\n    println!(\"Hello from {{project-name}}\");\n}\n");
        folder.Write("template/cargo-generate.toml", "[template]\ncargo_generate_version = \">=0.10.0\"\n");
        string branch;
        using (var repository = new Repository(template))
        {
            Commands.Stage(repository, "*");
            var signature = new Signature("test", "test@example.invalid", DateTimeOffset.UtcNow);
            repository.Commit("template", signature, signature);
            branch = repository.Head.FriendlyName;
        }

        var environment = frontend.CreateDxEnvironment(tools);
        environment["PATH"] = Directory.CreateDirectory(folder.Combine("no-programs")).FullName;
        if (OperatingSystem.IsWindows())
        {
            environment["PATH"] = frontend.Libgit2Directory + Path.PathSeparator + environment["PATH"];
        }

        var created = folder.Combine("created/gate-app");

        //Act
        var (exitCode, output) = Run(tools.DxPath, new[] { "new", "--template", new Uri(template).AbsoluteUri, "--branch", branch, "--yes", "--vcs", "none", created }, folder.Path, environment);

        //Assert
        exitCode.Should().Be(0, output);
        File.ReadAllText(Path.Combine(created, "Cargo.toml")).Should().Contain("name = \"gate-app\"");
        File.ReadAllText(Path.Combine(created, "src", "main.rs")).Should().Contain("Hello from gate-app");
    }
}
