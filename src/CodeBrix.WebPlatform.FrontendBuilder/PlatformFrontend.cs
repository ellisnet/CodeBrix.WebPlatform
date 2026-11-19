using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.LibGit2;
using CodeBrix.RustTools;
using CodeBrix.RustTools.Projects;
using CodeBrix.RustTools.Toolchain;
using CodeBrix.WebPlatform.BackendMapper;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using CodeBrix.WebPlatform.WasmShrink;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Builds one platform application's frontend. A build program creates one with the
/// application's <see cref="PlatformFrontendOptions"/> and calls, in this order:
/// <list type="number">
/// <item><see cref="CheckPrerequisitesAsync"/> - what is missing on this machine, and how to fix it;</item>
/// <item><see cref="MaterializeAsync"/> - writes the client code generated from the backend and the
/// shared Rust crate into the frontend, after which plain <c>cargo check/test/build</c> works there;</item>
/// <item><see cref="BuildAsync"/> - the servable site, or <see cref="ServeAsync"/> - <c>dx serve</c>
/// with hot reload.</item>
/// </list>
/// Nothing is ever installed. Every failure is a <see cref="FrontendBuildException"/> whose message
/// says what to do.
/// </summary>
public sealed class PlatformFrontend
{
    /// <summary>The text in dx's output that says wasm-opt ran and failed.</summary>
    internal const string ShrinkFailureText = "wasm-opt failed";

    private static readonly string[] WasmOptArguments =
    {
        "--enable-reference-types", "--enable-bulk-memory", "--enable-mutable-globals",
        "--enable-nontrapping-float-to-int", "--enable-threads", "--strip-debug",
    };

    private readonly ProcessRunner _runner;
    private readonly string _toolsBaseDirectory;

    /// <summary>
    /// Creates the builder for one frontend.
    /// </summary>
    /// <param name="options">The application's frontend and how to build it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><see cref="PlatformFrontendOptions.FrontendDirectory"/> is blank.</exception>
    public PlatformFrontend(PlatformFrontendOptions options)
        : this(options, AppContext.BaseDirectory)
    {
    }

    internal PlatformFrontend(PlatformFrontendOptions options, string toolsBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.FrontendDirectory))
        {
            throw new ArgumentException("PlatformFrontendOptions.FrontendDirectory must name the application's frontend folder.", nameof(options));
        }

        Options = options;
        _toolsBaseDirectory = toolsBaseDirectory;
        _runner = new ProcessRunner(options.Output);
    }

    /// <summary>The Tailwind program that is run (a name on PATH); tests name one that does not exist.</summary>
    internal string TailwindProgram { get; set; } = FrontendRequirements.TailwindProgram;

    /// <summary>The options this builder was created with.</summary>
    public PlatformFrontendOptions Options { get; }

    /// <summary>The frontend folder, as a full path.</summary>
    public string FrontendDirectory => Path.GetFullPath(Options.FrontendDirectory);

    /// <summary>The folder dx is told to keep its own data in (its <c>DX_HOME</c>).</summary>
    public string DxHomeDirectory
        => string.IsNullOrWhiteSpace(Options.DxHomeDirectory) ? Path.Combine(FrontendDirectory, "target", "dx-home") : Path.GetFullPath(Options.DxHomeDirectory);

    /// <summary>
    /// The build-owned folder that holds the libgit2 library for dx, under the name dx asks the
    /// loader for: <c>libgit2</c> in <see cref="DxHomeDirectory"/>. It is refreshed from the
    /// CodeBrix.LibGit2 package's file whenever dx is started.
    /// </summary>
    public string Libgit2Directory => Path.Combine(DxHomeDirectory, "libgit2");

    /// <summary>
    /// The arguments <see cref="BuildAsync"/> runs <c>dx</c> with. A release build passes
    /// <c>--debug-symbols false</c>: dx keeps debug symbols by default, also in a release build,
    /// and then tells wasm-opt to keep them too, which leaves the file about a quarter larger.
    /// Use these when building the same frontend somewhere else (in a container, for example).
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <returns>The arguments, starting with <c>build</c>.</returns>
    public static IReadOnlyList<string> GetDxBuildArguments(FrontendConfiguration configuration)
        => configuration == FrontendConfiguration.Release
            ? new[] { "build", "--platform", "web", "--release", "--debug-symbols", "false" }
            : new[] { "build", "--platform", "web" };

    /// <summary>
    /// Checks everything a build needs on this machine - Rust and its WebAssembly target, the
    /// standalone Tailwind program, wasm-opt, the dx and wasm-bindgen programs, and the libgit2
    /// library dx needs to start (from the CodeBrix.LibGit2 package; dx is started once, with
    /// <c>--version</c>, to prove it) - and says, for each, whether it is in place and what to
    /// do when it is not. Installs nothing and never throws for something missing.
    /// </summary>
    /// <param name="cancellationToken">Stops the probes.</param>
    /// <returns>One finding per <see cref="PrerequisiteKind"/>.</returns>
    public async Task<PrerequisiteReport> CheckPrerequisitesAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<PrerequisiteItem>();

        string rustVersion = null;
        try
        {
            var toolchain = await new RustToolchainDetector().DetectAsync(Directory.Exists(FrontendDirectory) ? FrontendDirectory : null, cancellationToken).ConfigureAwait(false);
            rustVersion = PrerequisiteRules.VersionText(toolchain.Rustc?.Version);
            items.Add(PrerequisiteRules.EvaluateRust(toolchain));
        }
        catch (RustToolsException exception)
        {
            items.Add(new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Missing, "The Rust toolchain could not be examined: " + exception.Message));
        }

        var tailwind = await _runner.RunAsync(TailwindProgram, new[] { "--help" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tailwindVersion = tailwind.NotFound ? null : FrontendRequirements.ParseTailwindVersion(tailwind.Output);
        items.Add(PrerequisiteRules.EvaluateTailwind(tailwindVersion, Options.MinimumTailwindVersion));

        var wasmOpt = await FindWasmOptAsync(cancellationToken).ConfigureAwait(false);
        items.Add(PrerequisiteRules.DescribeWasmOpt(wasmOpt, HostTools.CurrentRuntimeIdentifier));

        HostTools tools;
        string problem;
        try
        {
            problem = HostToolsLocator.TryLocate(_toolsBaseDirectory, HostTools.CurrentRuntimeIdentifier, Options.DxPath, Options.WasmBindgenPath, out tools);
        }
        catch (FrontendBuildException exception)
        {
            tools = null;
            problem = exception.Message;
        }

        items.Add(PrerequisiteRules.DescribeHostTools(tools, problem));

        string dxVersion = null;
        if (tools == null)
        {
            items.Add(PrerequisiteRules.DescribeLibgit2(null, "not checked: dx was not found.", null));
        }
        else
        {
            var libgit2Problem = PrepareLibgit2(tools, out var supply);
            if (libgit2Problem != null)
            {
                items.Add(PrerequisiteRules.DescribeLibgit2(null, libgit2Problem, null));
            }
            else
            {
                var environment = new Dictionary<string, string>(StringComparer.Ordinal);
                supply.ApplyTo(environment);
                var run = await _runner.RunAsync(tools.DxPath, new[] { "--version" }, environment: environment, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                dxVersion = run.Succeeded ? OutputText.FirstLine(run.Output) : null;
                items.Add(PrerequisiteRules.DescribeLibgit2(supply, null, run.Succeeded ? null : OutputText.Tail(run.Output, 5)));
            }
        }

        return new PrerequisiteReport(PrerequisiteRules.Ordered(items), rustVersion, tailwindVersion, wasmOpt, tools, dxVersion);
    }

    /// <summary>
    /// Finds the dx and wasm-bindgen programs: in the host-tools package's folder beside the
    /// build program (<see cref="HostTools.FolderName"/>) when it exists, otherwise at
    /// <see cref="PlatformFrontendOptions.DxPath"/> and <see cref="PlatformFrontendOptions.WasmBindgenPath"/>.
    /// On Linux and macOS they are given the execute permission when they lack it.
    /// </summary>
    /// <returns>The two programs.</returns>
    /// <exception cref="FrontendBuildException">They were not found; the message names the
    /// host-tools package to add for this machine.</exception>
    public HostTools FindHostTools()
    {
        var problem = HostToolsLocator.TryLocate(_toolsBaseDirectory, HostTools.CurrentRuntimeIdentifier, Options.DxPath, Options.WasmBindgenPath, out var tools);
        if (problem != null)
        {
            throw new FrontendBuildException(problem);
        }

        return tools;
    }

    /// <summary>
    /// Writes everything the frontend needs and does not write itself, so that plain
    /// <c>cargo check/test/build</c> (and an editor) work in the frontend folder afterwards:
    /// builds the backend in Release when <see cref="PlatformFrontendOptions.BackendProjectPath"/>
    /// is set, maps it with BackendMapper, writes the generated client code into
    /// <see cref="PlatformFrontendOptions.GeneratedClientDirectory"/>, the shared Rust crate into
    /// <see cref="PlatformFrontendOptions.SharedCrateDirectory"/>, the platform's crates into
    /// <see cref="PlatformFrontendOptions.ResolvedVendorDirectory"/> (for
    /// <see cref="CrateMode.PlatformCratesOnly"/>; unpacked only when the package's archive
    /// changed), and the cargo configuration into
    /// <see cref="PlatformFrontendOptions.CargoConfigurationFile"/>. A frontend without a
    /// <c>Cargo.lock</c> gets the platform's as its starting point. Files that already have the
    /// right content are not touched, so cargo does not rebuild for nothing. Last, when the
    /// frontend uses Tailwind and its generated stylesheet is missing, the installed Tailwind
    /// program writes it once (see <see cref="PlatformFrontendOptions.GenerateMissingStylesheet"/>);
    /// a missing or too old Tailwind is reported in the result, never thrown.
    /// </summary>
    /// <param name="cancellationToken">Stops the backend build.</param>
    /// <returns>The map of the backend and the files that changed.</returns>
    /// <exception cref="FrontendBuildException">The frontend folder or the backend is missing,
    /// the backend does not build, its contract is not fully declared (the message lists every
    /// problem; the inner exception is BackendMapper's), an output folder or the cargo
    /// configuration holds files this library did not write, or the platform's crates are not
    /// beside the build program.</exception>
    public async Task<MaterializeResult> MaterializeAsync(CancellationToken cancellationToken = default)
    {
        var frontend = RequireFrontend();
        var assembly = await PrepareBackendAsync(cancellationToken).ConfigureAwait(false);

        BackendMap map;
        IReadOnlyList<string> generated;
        try
        {
            map = CodeBrix.WebPlatform.BackendMapper.BackendMapper.Map(new BackendMapOptions { AssemblyPath = assembly, Types = Options.Types });
            var files = RustClientGenerator.Generate(map, Options.ClientOptions);
            _runner.Say($"  mapped {map.Controllers.Sum(c => c.Endpoints.Count)} endpoints in {map.Controllers.Count} controllers, {map.Hubs.Count} hubs, {map.Structs.Count} contract classes and {map.Enums.Count} enums.");
            generated = RustClientGenerator.WriteTo(Options.GeneratedClientDirectory, files);
        }
        catch (BackendMapException exception)
        {
            throw new FrontendBuildException("The client code could not be generated from the backend. " + exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new FrontendBuildException(exception.Message, exception);
        }

        IReadOnlyList<string> shared;
        try
        {
            shared = SharedRustCrate.ExtractTo(Options.SharedCrateDirectory);
        }
        catch (InvalidOperationException exception)
        {
            throw new FrontendBuildException(exception.Message, exception);
        }

        var (vendorChanged, configurationWritten, lockWritten) = MaterializeCrates(frontend);
        var (stylesheet, stylesheetMessage) = await EnsureStylesheetAsync(cancellationToken).ConfigureAwait(false);
        var result = new MaterializeResult(map, generated, shared, vendorChanged, configurationWritten, lockWritten, stylesheet, stylesheetMessage);
        _runner.Say(result.WasUpToDate
            ? "  the generated client code, the shared crate and the crates were already up to date."
            : $"  wrote {generated.Count} generated file(s) and {shared.Count} shared-crate file(s)"
                + (vendorChanged ? (Options.CrateMode == CrateMode.PlatformCratesOnly ? ", the platform's crates" : ", removed the platform's vendor folder") : string.Empty)
                + (configurationWritten ? ", the cargo configuration" : string.Empty)
                + (lockWritten ? ", a starting Cargo.lock" : string.Empty)
                + (stylesheet == StylesheetOutcome.Generated ? ", the Tailwind stylesheet" : string.Empty)
                + ".");
        if (result.StylesheetMissing)
        {
            _runner.Say("  " + stylesheetMessage);
        }

        return result;
    }

    /// <summary>
    /// Writes the generated Tailwind stylesheet when the frontend uses Tailwind and the file is
    /// missing, by starting the installed Tailwind program once in the frontend folder. Never
    /// throws for a missing, too old or failing Tailwind: the outcome says what happened.
    /// </summary>
    internal async Task<(StylesheetOutcome Outcome, string Message)> EnsureStylesheetAsync(CancellationToken cancellationToken)
    {
        var input = Options.ResolvedTailwindInputFile;
        var output = Options.ResolvedTailwindOutputFile;
        var decided = StylesheetRules.Decide(Options.GenerateMissingStylesheet, File.Exists(input), File.Exists(output));
        if (decided != null)
        {
            return (decided.Value, StylesheetRules.Describe(decided.Value, input, output));
        }

        var probe = await _runner.RunAsync(TailwindProgram, new[] { "--help" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var problem = StylesheetRules.TailwindProblem(probe.NotFound ? null : FrontendRequirements.ParseTailwindVersion(probe.Output), Options.MinimumTailwindVersion);
        if (problem != null)
        {
            return (StylesheetOutcome.SkippedTailwindNotAvailable, StylesheetRules.NotGenerated(output, problem));
        }

        _runner.Say("  generating the missing Tailwind stylesheet ...");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var run = await _runner.RunAsync(TailwindProgram, new[] { "-i", input, "-o", output }, FrontendDirectory, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded || !File.Exists(output))
        {
            var reason = "the Tailwind program failed:" + Environment.NewLine + OutputText.Tail(run.Output, 20);
            return (StylesheetOutcome.SkippedTailwindFailed, StylesheetRules.NotGenerated(output, reason));
        }

        return (StylesheetOutcome.Generated, StylesheetRules.Describe(StylesheetOutcome.Generated, input, output));
    }

    /// <summary>
    /// The vendor folder (when <see cref="CrateMode.PlatformCratesOnly"/>), the cargo
    /// configuration, and a starting Cargo.lock when the frontend has none.
    /// </summary>
    private (bool VendorChanged, bool ConfigurationWritten, bool LockWritten) MaterializeCrates(string frontend)
    {
        var vendor = Options.ResolvedVendorDirectory;
        bool vendorChanged;
        if (Options.CrateMode == CrateMode.PlatformCratesOnly)
        {
            var archive = PlatformCrates.GetArchivePath(_toolsBaseDirectory);
            if (!File.Exists(Path.Combine(vendor, PlatformCrates.StampFileName)))
            {
                _runner.Say("  writing the platform's crates into the frontend ...");
            }

            vendorChanged = PlatformCrates.ExtractTo(archive, vendor);
        }
        else
        {
            vendorChanged = PlatformCrates.Remove(vendor);
        }

        var configuration = CargoConfiguration.Create(Options.CrateMode, CargoConfiguration.VendorPath(frontend, vendor));
        var configurationWritten = CargoConfiguration.Write(Options.CargoConfigurationFile, configuration);

        var lockFile = Path.Combine(frontend, "Cargo.lock");
        var lockWritten = false;
        if (!File.Exists(lockFile))
        {
            File.WriteAllText(lockFile, PlatformCrates.LockFileText, new UTF8Encoding(false));
            lockWritten = true;
        }

        return (vendorChanged, configurationWritten, lockWritten);
    }

    /// <summary>
    /// Builds the servable site with dx, from a clean dx output folder. A release build is shrunk
    /// by wasm-opt, which dx runs - the packaged one from CodeBrix.WebPlatform.WasmShrink first on
    /// PATH, an installed one as the fallback - and fails if dx reports that wasm-opt failed (dx
    /// itself only logs that). A debug build is not shrunk. <see cref="PlatformFrontendOptions.Shrink"/>
    /// changes either: <c>true</c> shrinks a debug build (this library runs wasm-opt on dx's
    /// output), <c>false</c> leaves a release build unshrunk (dx is told to skip wasm-opt, and
    /// no wasm-opt is needed). <see cref="FrontendBuildResult.Shrunk"/> says what happened. dx
    /// is started with the libgit2 library it needs (see <see cref="Libgit2Directory"/>). Call
    /// <see cref="MaterializeAsync"/> first.
    /// </summary>
    /// <param name="configuration">Release (the build that ships) or Debug.</param>
    /// <param name="webRootDirectory">A folder to copy the built site into, replacing what it
    /// held (a <c>.gitkeep</c> file is kept), or <c>null</c> to leave the site where dx put it.</param>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>Where the site is, and how it was built.</returns>
    /// <exception cref="FrontendBuildException">A program or the libgit2 library is missing, dx
    /// failed, or wasm-opt failed.</exception>
    public async Task<FrontendBuildResult> BuildAsync(FrontendConfiguration configuration = FrontendConfiguration.Release, string webRootDirectory = null, CancellationToken cancellationToken = default)
    {
        var plan = ShrinkPlan.Decide(configuration, Options.Shrink);
        var frontend = RequireFrontend();
        var tools = FindHostTools();
        var environment = CreateDxEnvironment(tools);
        environment[ShrinkPlan.SkipVariable] = plan.SkipVariableValue;
        var source = plan.NeedsWasmOpt || plan.SkipRequested ? await FindWasmOptAsync(cancellationToken).ConfigureAwait(false) : WasmOptSource.Missing;
        if (plan.NeedsWasmOpt && source == WasmOptSource.Missing)
        {
            throw new FrontendBuildException(PrerequisiteRules.DescribeWasmOpt(source, HostTools.CurrentRuntimeIdentifier).Message);
        }

        var dxOutput = Path.Combine(frontend, "target", "dx");
        if (Directory.Exists(dxOutput))
        {
            Directory.Delete(dxOutput, recursive: true);
        }

        _runner.Say(configuration == FrontendConfiguration.Release
            ? (plan.SkipRequested ? "Building the frontend, unshrunk (cargo, wasm-bindgen, Tailwind; Shrink is false) ..." : "Building the frontend (cargo, wasm-bindgen, wasm-opt, Tailwind) ...")
            : "Building the frontend for development (cargo, wasm-bindgen, Tailwind) ...");
        var result = await _runner.RunAsync(tools.DxPath, GetDxBuildArguments(configuration), frontend, environment, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var output = GetOutputDirectory(configuration);
        if (!result.Succeeded || !File.Exists(Path.Combine(output, "index.html")))
        {
            throw new FrontendBuildException("The frontend build failed:" + Environment.NewLine + OutputText.Tail(result.Output, 60));
        }

        var dxShrank = plan.DxRanWasmOpt(configuration, result.Output);
        if (dxShrank && ReportsShrinkFailure(result.Output))
        {
            throw new FrontendBuildException("dx built the frontend, but wasm-opt failed and the WebAssembly file was left unshrunk:" + Environment.NewLine + OutputText.Tail(result.Output, 60));
        }

        if (plan.LibraryShrinks)
        {
            await ShrinkFilesAsync(output, source, cancellationToken).ConfigureAwait(false);
        }

        var shrunk = dxShrank || plan.LibraryShrinks;

        var copied = 0;
        string webRoot = null;
        if (webRootDirectory != null)
        {
            webRoot = Path.GetFullPath(webRootDirectory);
            copied = CopySite(output, webRoot);
        }

        return new FrontendBuildResult(configuration, output, shrunk, shrunk ? source : WasmOptSource.Missing, tools, webRoot, copied);
    }

    /// <summary>
    /// Runs <c>dx serve</c> with hot reload in the frontend folder, with the same environment as
    /// <see cref="BuildAsync"/> (the libgit2 library included) and the features in <see cref="PlatformFrontendOptions.ServeFeatures"/>,
    /// until it ends or is cancelled. Its output goes to <see cref="PlatformFrontendOptions.Output"/>.
    /// Call <see cref="MaterializeAsync"/> first.
    /// </summary>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>dx's exit code.</returns>
    /// <exception cref="FrontendBuildException">The frontend folder, a program or the libgit2 library is missing.</exception>
    public async Task<int> ServeAsync(CancellationToken cancellationToken = default)
    {
        var frontend = RequireFrontend();
        var tools = FindHostTools();
        var arguments = new List<string> { "serve", "--platform", "web" };
        if (Options.ServeFeatures.Count > 0)
        {
            arguments.Add("--features");
            arguments.Add(string.Join(",", Options.ServeFeatures));
        }

        _runner.Say("Starting dx serve (Ctrl+C stops it) ...");
        var result = await _runner.RunAsync(tools.DxPath, arguments, frontend, CreateDxEnvironment(tools), cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    /// <summary>
    /// Where dx leaves the site for a configuration:
    /// <c>target/dx/&lt;package name&gt;/&lt;release|debug&gt;/web/public</c> in the frontend folder.
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <returns>The folder, as a full path.</returns>
    /// <exception cref="FrontendBuildException">The frontend's Cargo.toml is missing or names no package.</exception>
    public string GetOutputDirectory(FrontendConfiguration configuration)
    {
        var frontend = RequireFrontend();
        string name;
        try
        {
            name = CargoManifest.Load(Path.Combine(frontend, CargoManifest.FileName)).Package?.Name;
        }
        catch (Exception exception) when (exception is RustToolsException or IOException or FormatException)
        {
            throw new FrontendBuildException($"The frontend's {CargoManifest.FileName} could not be read: {exception.Message}", exception);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FrontendBuildException($"The frontend's {CargoManifest.FileName} has no [package] name.");
        }

        return Path.Combine(frontend, "target", "dx", name, configuration == FrontendConfiguration.Release ? "release" : "debug", "web", "public");
    }

    /// <summary>
    /// The environment dx runs with. PATH starts with the folder of the packaged wasm-opt (when
    /// the package has one for this machine) and then the folder of wasm-bindgen, so those are
    /// the ones dx finds; DX_HOME is <see cref="DxHomeDirectory"/>; and the folder holding the
    /// libgit2 library dx needs to start (<see cref="Libgit2Directory"/>) goes first on the
    /// loader's search variable - LD_LIBRARY_PATH on Linux, DYLD_LIBRARY_PATH on macOS, PATH on
    /// Windows. Only dx's process gets these.
    /// </summary>
    /// <exception cref="FrontendBuildException">libgit2 could not be supplied.</exception>
    internal Dictionary<string, string> CreateDxEnvironment(HostTools tools)
    {
        var path = tools.WasmBindgenDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        if (WasmShrinkTool.IsAvailable)
        {
            path = WasmShrinkTool.GetToolDirectory() + Path.PathSeparator + path;
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = path,
            ["DX_HOME"] = DxHomeDirectory,
        };
        var problem = PrepareLibgit2(tools, out var supply);
        if (problem != null)
        {
            throw new FrontendBuildException(problem);
        }

        supply.ApplyTo(environment);
        return environment;
    }

    /// <summary>
    /// Prepares the libgit2 library for a dx (<see cref="Libgit2Supply.Prepare"/>), with the
    /// commit and version the CodeBrix.LibGit2 package reports for itself.
    /// </summary>
    /// <returns><c>null</c> when prepared, otherwise what to do.</returns>
    internal string PrepareLibgit2(HostTools tools, out Libgit2Supply supply)
    {
        var version = GlobalSettings.Version;
        var packageVersion = version.InformationalVersion ?? string.Empty;
        var plus = packageVersion.IndexOf('+');
        return Libgit2Supply.Prepare(
            tools.DxPath,
            tools.RuntimeIdentifier ?? HostTools.CurrentRuntimeIdentifier,
            _toolsBaseDirectory,
            Libgit2Directory,
            version.LibGit2CommitSha,
            plus < 0 ? packageVersion : packageVersion.Substring(0, plus),
            out supply);
    }

    /// <summary>
    /// True when dx's output says that wasm-opt ran and failed. dx does not treat that as an
    /// error: it logs it, keeps the unshrunk file and reports a successful build.
    /// </summary>
    /// <param name="dxOutput">Everything dx wrote.</param>
    internal static bool ReportsShrinkFailure(string dxOutput)
        => (dxOutput ?? string.Empty).Contains(ShrinkFailureText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Replaces a web-root folder's content with the built site. A <c>.gitkeep</c> file in it is kept.
    /// </summary>
    /// <param name="site">The built site.</param>
    /// <param name="webRoot">The folder to fill; created when missing.</param>
    /// <returns>The number of files copied.</returns>
    internal static int CopySite(string site, string webRoot)
    {
        Directory.CreateDirectory(webRoot);
        foreach (var entry in Directory.EnumerateFileSystemEntries(webRoot))
        {
            if (Path.GetFileName(entry) == ".gitkeep")
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(webRoot, Path.GetRelativePath(site, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, overwrite: true);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Says where the wasm-opt that shrinks the WebAssembly comes from: the
    /// CodeBrix.WebPlatform.WasmShrink package, else one installed on PATH, else none.
    /// </summary>
    internal async Task<WasmOptSource> FindWasmOptAsync(CancellationToken cancellationToken)
    {
        if (WasmShrinkTool.IsAvailable)
        {
            return WasmOptSource.Package;
        }

        var installed = await _runner.RunAsync(WasmShrinkTool.ToolName, new[] { "--version" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        return installed.Succeeded ? WasmOptSource.Installed : WasmOptSource.Missing;
    }

    private string RequireFrontend()
    {
        var frontend = FrontendDirectory;
        if (!File.Exists(Path.Combine(frontend, CargoManifest.FileName)))
        {
            throw new FrontendBuildException($"'{frontend}' is not a frontend folder: it has no {CargoManifest.FileName}.");
        }

        return frontend;
    }

    private async Task<string> PrepareBackendAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Options.BackendProjectPath))
        {
            if (string.IsNullOrWhiteSpace(Options.BackendAssemblyPath))
            {
                throw new FrontendBuildException("Name the backend in the options: BackendProjectPath (built in Release, then read) or BackendAssemblyPath (read as it is).");
            }

            return Path.GetFullPath(Options.BackendAssemblyPath);
        }

        var project = Path.GetFullPath(Options.BackendProjectPath);
        if (!File.Exists(project))
        {
            throw new FrontendBuildException($"The backend project '{project}' does not exist.");
        }

        _runner.Say("Building the backend to read its REST endpoints and hubs ...");
        var build = await _runner.RunAsync(
            "dotnet",
            new[] { "build", project, "--configuration", "Release", "--nologo", "--verbosity", "quiet" },
            Path.GetDirectoryName(project),
            echo: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!build.Succeeded)
        {
            throw new FrontendBuildException("Building the backend failed:" + Environment.NewLine + OutputText.Tail(build.Output, 40));
        }

        var assembly = string.IsNullOrWhiteSpace(Options.BackendAssemblyPath)
            ? await ResolveTargetPathAsync(project, cancellationToken).ConfigureAwait(false)
            : Path.GetFullPath(Options.BackendAssemblyPath);
        if (!File.Exists(assembly))
        {
            throw new FrontendBuildException($"The backend was built, but '{assembly}' does not exist. Set BackendAssemblyPath to the assembly the build produces.");
        }

        return assembly;
    }

    /// <summary>Asks MSBuild where the Release build of a project puts its assembly.</summary>
    internal async Task<string> ResolveTargetPathAsync(string project, CancellationToken cancellationToken)
    {
        var query = await _runner.RunAsync(
            "dotnet",
            new[] { "msbuild", project, "-getProperty:TargetPath", "-property:Configuration=Release", "-nologo" },
            Path.GetDirectoryName(project),
            echo: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var path = query.Succeeded
            ? query.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
            : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FrontendBuildException($"Where the build of '{project}' puts its assembly could not be worked out (a project with several target frameworks has no single one). Set BackendAssemblyPath.");
        }

        return path;
    }

    private async Task ShrinkFilesAsync(string site, WasmOptSource source, CancellationToken cancellationToken)
    {
        var program = source == WasmOptSource.Package ? WasmShrinkTool.GetExecutablePath() : WasmShrinkTool.ToolName;
        foreach (var file in Directory.EnumerateFiles(site, "*.wasm", SearchOption.AllDirectories).ToList())
        {
            _runner.Say($"Shrinking {Path.GetFileName(file)} ...");
            var arguments = new List<string> { file, "-Oz", "-o", file };
            arguments.AddRange(WasmOptArguments);
            var result = await _runner.RunAsync(program, arguments, site, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new FrontendBuildException($"wasm-opt failed on '{file}':" + Environment.NewLine + OutputText.Tail(result.Output, 30));
            }
        }
    }
}
