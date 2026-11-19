using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.LibGit2;
using CodeBrix.RustTools.Docker;
using CodeBrix.RustTools.Docker.Containers;
using CodeBrix.RustTools.Docker.Images;
using CodeBrix.RustTools.Projects;
using CodeBrix.WebPlatform.FrontendBuilder;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using CodeBrix.WebPlatform.WasmShrink;

namespace CodeBrix.WebPlatform.LibraryChecker.Maintenance;

/// <summary>
/// A maintainer test of the platform: it proves that a materialized platform frontend builds
/// without Node.js and without the network, with ONLY the packaged tools. It builds the frontend
/// inside a throw-away container that has Rust but no Node, npm or any other JavaScript runtime,
/// with the container's network switched off; dx and wasm-bindgen are the Linux programs of the
/// host-tools packages, wasm-opt is the WasmShrink package's and the libgit2 library dx needs to
/// start is the CodeBrix.LibGit2 package's (under the name dx asks for, on LD_LIBRARY_PATH), all
/// copied in, and the crates are the platform's, materialized into the frontend. A build on a developer's machine cannot
/// prove this, because that machine may have Node installed. It is no part of any package (the
/// packages have no container dependency).
/// </summary>
public sealed class NodeFreeProof
{
    /// <summary>The folder, below the container's work folder, the frontend is copied into.</summary>
    public const string ContainerFrontend = "frontend";

    /// <summary>
    /// The paths of the frontend folder that are NOT copied into the container: cargo's and dx's
    /// output. Every pattern starts with a slash, which anchors it to the frontend folder: a bare
    /// name such as "target" would also drop folders of that name inside the vendored crates, and
    /// cargo refuses a vendored crate with a missing file.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedFromContainer = new[] { "/target" };

    /// <summary>
    /// The shell command that fails when any JavaScript runtime or package manager is installed
    /// in the container.
    /// </summary>
    public const string NoNodeProbe = "for tool in node nodejs npm npx yarn pnpm bun deno; do if command -v \"$tool\" >/dev/null 2>&1; then echo \"FOUND $tool\"; exit 1; fi; done; echo none";

    /// <summary>
    /// The shell command that fails when the image itself has a <c>wasm-opt</c>, <c>dx</c> or
    /// <c>wasm-bindgen</c>: the proof must use the packaged ones, which are copied in and put
    /// first on PATH afterwards.
    /// </summary>
    public const string NoInstalledToolsProbe = "for tool in wasm-opt dx wasm-bindgen; do if command -v \"$tool\" >/dev/null 2>&1; then echo \"FOUND $(command -v $tool)\"; exit 1; fi; done; echo none";

    private readonly TextWriter _output;

    /// <summary>
    /// Creates the proof for a frontend.
    /// </summary>
    /// <param name="frontendDirectory">The materialized frontend folder.</param>
    /// <param name="output">Where progress lines go.</param>
    public NodeFreeProof(string frontendDirectory, TextWriter output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendDirectory);
        FrontendDirectory = Path.GetFullPath(frontendDirectory);
        _output = output ?? TextWriter.Null;
    }

    /// <summary>The frontend folder the proof builds.</summary>
    public string FrontendDirectory { get; }

    /// <summary>
    /// The frontend the proof builds by default: the repository's sample application, whose
    /// frontend uses every part of the platform. The proof deliberately takes the sample as its
    /// subject; it checks nothing of the sample's own.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static string DefaultFrontend(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Path.Combine(layout.Samples, "WorkSite", "frontend");
    }

    /// <summary>
    /// The runtime identifier of the Linux container Docker starts on this machine: Linux, with
    /// the machine's own processor architecture.
    /// </summary>
    /// <exception cref="MaintenanceException">The processor architecture is not one the platform builds on.</exception>
    public static string ContainerRuntimeIdentifier()
        => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "linux-x64",
            Architecture.Arm64 => "linux-arm64",
            var other => throw new MaintenanceException($"The proof build has no Linux tools for a container on a {other} machine."),
        };

    /// <summary>
    /// What is missing for the frontend to build from its own folder alone: the materialized
    /// shared crate, generated client code, vendor folder and cargo configuration.
    /// </summary>
    /// <returns>One line per missing piece; empty when the frontend is materialized.</returns>
    public IReadOnlyList<string> MissingMaterializedFiles()
    {
        var expected = new[]
        {
            Path.Combine("platform", "codebrix-webplatform", "Cargo.toml"),
            Path.Combine("platform", "vendor", PlatformCrates.StampFileName),
            Path.Combine(".cargo", "config.toml"),
            Path.Combine("src", "generated", "mod.rs"),
        };
        return expected.Where(p => !File.Exists(Path.Combine(FrontendDirectory, p))).Select(p => p.Replace('\\', '/')).ToList();
    }

    /// <summary>
    /// Runs the proof build.
    /// </summary>
    /// <param name="cancellationToken">Stops the proof.</param>
    /// <returns>The sizes and names of the files the container build produced.</returns>
    /// <exception cref="MaintenanceException">The frontend is not materialized, a packaged tool
    /// is missing, Docker is unavailable, or a step in the container failed.</exception>
    public async Task<IReadOnlyList<string>> RunAsync(CancellationToken cancellationToken = default)
    {
        var missing = MissingMaterializedFiles();
        if (missing.Count > 0)
        {
            throw new MaintenanceException(
                $"The frontend '{FrontendDirectory}' is not materialized (missing: {string.Join(", ", missing)}). Materialize it with its build program first, in the default crate mode (for the sample: `dotnet run --project build/WorkSite.BuildFrontend -- generate` in samples/WorkSite).");
        }

        var package = CargoManifest.Load(Path.Combine(FrontendDirectory, CargoManifest.FileName)).Package?.Name
            ?? throw new MaintenanceException("The frontend's Cargo.toml has no [package] name.");
        var runtime = ContainerRuntimeIdentifier();
        var hostTools = Path.Combine(AppContext.BaseDirectory, HostTools.FolderName, runtime);
        foreach (var program in new[] { HostTools.DxName, HostTools.WasmBindgenName })
        {
            if (!File.Exists(Path.Combine(hostTools, program)))
            {
                throw new MaintenanceException($"The packaged {program} for {runtime} is not beside the maintainer tool ({hostTools}); build the solution first.");
            }
        }

        var tailwind = LocateTailwind();
        var options = new DockerRustWorkbenchOptions();
        // The vendored crate folders must arrive complete: a crate may have a folder named
        // "target" of its own, and cargo checks every vendored file. Nothing is excluded by
        // name; the exclusions are the explicit paths in ExcludedFromContainer.
        options.DefaultExcludePatterns.Clear();
        using var workbench = new DockerRustWorkbench(options);
        try
        {
            _output.WriteLine("Preparing the build image (Rust and the WebAssembly target; no Node.js) ...");
            var spec = new BuildImageSpec();
            spec.ExtraDockerfileLines.Add("RUN rustup target add wasm32-unknown-unknown");
            var image = await workbench.EnsureImageAsync(spec, cancellationToken: cancellationToken).ConfigureAwait(false);

            _output.WriteLine("Starting a container with its network switched off ...");
            await using var container = await workbench.StartContainerAsync(image, new BuildContainerOptions { NetworkDisabled = true }, cancellationToken).ConfigureAwait(false);

            var probe = await workbench.ExecAsync(container, Shell(NoNodeProbe), cancellationToken).ConfigureAwait(false);
            _output.WriteLine($"  JavaScript runtimes and package managers in the container: {probe.Trim()}");
            var installed = await workbench.ExecAsync(container, Shell(NoInstalledToolsProbe), cancellationToken).ConfigureAwait(false);
            _output.WriteLine($"  wasm-opt, dx or wasm-bindgen installed in the image itself: {installed.Trim()}");

            _output.WriteLine("Copying the materialized frontend (its crates included) into the container ...");
            var copy = await workbench.CopySourceIntoAsync(container, FrontendDirectory, ContainerFrontend, ExcludedFromContainer, cancellationToken: cancellationToken).ConfigureAwait(false);
            _output.WriteLine($"  {copy.FileCount} files copied.");

            var staging = Path.Combine(Path.GetTempPath(), "webplatform-proof-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                File.Copy(tailwind, Path.Combine(staging, "tailwindcss"));
                await workbench.CopySourceIntoAsync(container, staging, "tailwind-bin", cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Directory.Delete(staging, recursive: true);
            }

            _output.WriteLine($"Copying the packaged wasm-opt, dx, wasm-bindgen and libgit2 ({runtime}) into the container ...");
            await workbench.CopySourceIntoAsync(container, WasmShrinkTool.GetToolDirectory(runtime), "wasm-opt-bin", cancellationToken: cancellationToken).ConfigureAwait(false);
            await workbench.CopySourceIntoAsync(container, hostTools, "frontend-tools", cancellationToken: cancellationToken).ConfigureAwait(false);
            var libgit2 = Path.Combine(Path.GetTempPath(), "webplatform-proof-libgit2-" + Guid.NewGuid().ToString("N"));
            try
            {
                var supplied = PrepareLibgit2(Path.Combine(hostTools, HostTools.DxName), runtime, libgit2);
                _output.WriteLine($"  libgit2: {supplied}");
                await workbench.CopySourceIntoAsync(container, libgit2, "libgit2", cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (Directory.Exists(libgit2))
                {
                    Directory.Delete(libgit2, recursive: true);
                }
            }

            await workbench.ExecAsync(container, new[] { "chmod", "+x", "/work/tailwind-bin/tailwindcss", "/work/wasm-opt-bin/wasm-opt", "/work/frontend-tools/dx", "/work/frontend-tools/wasm-bindgen" }, cancellationToken).ConfigureAwait(false);
            var versions = await workbench.ExecAsync(container, Shell($"LD_LIBRARY_PATH={ContainerLibgit2} /work/frontend-tools/dx --version && /work/frontend-tools/wasm-bindgen --version && /work/wasm-opt-bin/wasm-opt --version"), cancellationToken).ConfigureAwait(false);
            foreach (var line in versions.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _output.WriteLine("  " + line);
            }

            _output.WriteLine("Building the frontend in the container (offline, from its materialized crates) ...");
            await workbench.ExecAsync(
                container,
                Shell($"cd {ContainerFrontend} && PATH=/work/wasm-opt-bin:/work/frontend-tools:/work/tailwind-bin:$PATH DX_HOME=/work/dx-home LD_LIBRARY_PATH={ContainerLibgit2} dx {string.Join(" ", PlatformFrontend.GetDxBuildArguments(FrontendConfiguration.Release))}"),
                cancellationToken).ConfigureAwait(false);

            var listing = await workbench.ExecAsync(
                container,
                Shell($"cd {ContainerFrontend}/target/dx/{package}/release/web/public && test -f index.html && find . -type f -printf '%s %p\\n' | sort -k2"),
                cancellationToken).ConfigureAwait(false);
            var files = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (!files.Any(f => f.EndsWith(".wasm", StringComparison.Ordinal)))
            {
                throw new MaintenanceException("The container build finished but produced no WebAssembly file.");
            }

            return files;
        }
        catch (DockerRustToolsException exception)
        {
            throw new MaintenanceException("The Node-free proof build failed: " + exception.Message + Environment.NewLine + Tail(Details(exception), 40), exception);
        }
    }

    /// <summary>The folder in the container that holds libgit2 for dx (on its LD_LIBRARY_PATH).</summary>
    public const string ContainerLibgit2 = "/work/libgit2";

    /// <summary>
    /// Puts the libgit2 library the container's dx needs into a folder, under the name dx asks
    /// the loader for, from the CodeBrix.LibGit2 package beside the maintainer tool - the same
    /// preparation the FrontendBuilder library makes before every start of dx.
    /// </summary>
    /// <returns>A line that says what was prepared.</returns>
    private static string PrepareLibgit2(string dx, string runtime, string folder)
    {
        var version = GlobalSettings.Version;
        var problem = Libgit2Supply.Prepare(dx, runtime, AppContext.BaseDirectory, folder, version.LibGit2CommitSha, version.InformationalVersion, out var supply);
        if (problem != null)
        {
            throw new MaintenanceException(problem);
        }

        if (!supply.IsNeeded)
        {
            Directory.CreateDirectory(folder);
            return "the packaged dx does not link libgit2; nothing supplied";
        }

        return $"{supply.LoaderFileName} from {supply.SourceFile} ({Libgit2Supply.PackageId})";
    }

    private static string[] Shell(string command) => new[] { "sh", "-c", command };

    private static string Details(DockerRustToolsException exception)
        => exception is ContainerBuildException build ? build.StandardError + Environment.NewLine + build.StandardOutput : string.Empty;

    private static string Tail(string text, int lines)
    {
        var all = (text ?? string.Empty).Split('\n').Where(l => l.Trim().Length > 0).ToList();
        return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Count - lines)).Select(l => "    " + l));
    }

    private static string LocateTailwind()
    {
        var name = FrontendRequirements.TailwindProgram + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate) && FrontendRequirements.ParseTailwindVersion(Run(candidate, "--help")) != null)
            {
                return candidate;
            }
        }

        throw new MaintenanceException("The standalone Tailwind program was not found on PATH; the proof build copies it into the container.");
    }

    private static string Run(string program, string argument)
    {
        try
        {
            var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return text;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
