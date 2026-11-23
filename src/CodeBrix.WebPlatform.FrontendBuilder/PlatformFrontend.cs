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
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;
using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using CodeBrix.WebPlatform.WasmShrink;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Builds a platform application's frontend: one frontend application, or several that share
/// the backend (<see cref="PlatformFrontendOptions.Applications"/>). A build program creates one
/// with the application's <see cref="PlatformFrontendOptions"/> and calls, in this order:
/// <list type="number">
/// <item><see cref="CheckPrerequisitesAsync"/> - what is missing on this machine, and how to fix it;</item>
/// <item><see cref="MaterializeAsync"/> - writes the client code generated from the backend, the
/// shared Rust crate and the crates into every frontend, after which plain
/// <c>cargo check/test/build</c> works there;</item>
/// <item><see cref="BuildAsync"/> - the servable site of every application, or
/// <see cref="ServeAsync(string, CancellationToken)"/> - <c>dx serve</c> with hot reload for one.</item>
/// </list>
/// A package that brings Rust crates of its own is registered by the build program before
/// <see cref="MaterializeAsync"/> (<see cref="CrateBundles.Register(CrateBundle)"/>, usually
/// through a call the package offers). Nothing is ever installed. Every failure is a
/// <see cref="FrontendBuildException"/> whose message says what to do.
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
    private readonly IReadOnlyList<ApplicationPlan> _plans;

    /// <summary>
    /// Creates the builder for a platform application's frontend.
    /// </summary>
    /// <param name="options">The frontend application or applications and how to build them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">No frontend folder is named; both
    /// <see cref="PlatformFrontendOptions.FrontendDirectory"/> and
    /// <see cref="PlatformFrontendOptions.Applications"/> are used; an application's name breaks
    /// a rule or is used twice; more than one application has no name; or two applications name
    /// the same folder. The message says which.</exception>
    public PlatformFrontend(PlatformFrontendOptions options)
        : this(options, AppContext.BaseDirectory)
    {
    }

    internal PlatformFrontend(PlatformFrontendOptions options, string toolsBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _plans = ApplicationPlan.Create(options);
        Applications = new FrontendApplicationSet(_plans.Select(p => p.Application));
        Options = options;
        _toolsBaseDirectory = toolsBaseDirectory;
        _runner = new ProcessRunner(options.Output);
    }

    /// <summary>The crate bundles to use instead of the registered ones; tests set it.</summary>
    internal IReadOnlyList<CrateBundle> CrateBundlesOverride { get; set; }

    /// <summary>The options this builder was created with.</summary>
    public PlatformFrontendOptions Options { get; }

    /// <summary>
    /// The frontend applications this builder builds, in the order of the options: one, or the
    /// entries of <see cref="PlatformFrontendOptions.Applications"/>.
    /// </summary>
    public FrontendApplicationSet Applications { get; }

    /// <summary>The frontend folder (the first application's), as a full path.</summary>
    public string FrontendDirectory => _plans[0].FrontendDirectory;

    /// <summary>The folder dx is told to keep its own data in (its <c>DX_HOME</c>), for the first application.</summary>
    public string DxHomeDirectory => _plans[0].DxHomeDirectory;

    /// <summary>
    /// The build-owned folder that holds the libgit2 library for dx, under the name dx asks the
    /// loader for: <c>libgit2</c> in <see cref="DxHomeDirectory"/>. It is refreshed from the
    /// CodeBrix.LibGit2 package's file whenever dx is started.
    /// </summary>
    public string Libgit2Directory => Path.Combine(DxHomeDirectory, "libgit2");

    /// <summary>The frontend folder of an application, as a full path.</summary>
    /// <param name="applicationName">The application's name; <c>null</c> or empty for the one without a name.</param>
    /// <returns>The folder.</returns>
    /// <exception cref="FrontendBuildException">There is no application of that name.</exception>
    public string GetFrontendDirectory(string applicationName) => FindPlan(applicationName, forServe: false).FrontendDirectory;

    /// <summary>
    /// The arguments <see cref="BuildAsync"/> runs <c>dx</c> with. A release build passes
    /// <c>--debug-symbols false</c>: dx keeps debug symbols by default, also in a release build,
    /// and then tells wasm-opt to keep them too, which leaves the file about a quarter larger.
    /// Use these when building the same frontend somewhere else (in a container, for example).
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <returns>The arguments, starting with <c>build</c>.</returns>
    public static IReadOnlyList<string> GetDxBuildArguments(FrontendConfiguration configuration)
        => GetDxBuildArguments(configuration, null, false);

    /// <summary>
    /// The arguments <see cref="BuildAsync"/> runs <c>dx</c> with for one application: those of
    /// <see cref="GetDxBuildArguments(FrontendConfiguration)"/>, then <c>--base-path &lt;name&gt;</c>
    /// for an application that has a name (its site is served below that name), then
    /// <c>--wasm-split</c> when the application is built with bundle splitting.
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <param name="applicationName">The application's name; <c>null</c>, empty or "app" for the
    /// application at the site root.</param>
    /// <param name="wasmSplit">Whether the bundle is split (<see cref="PlatformFrontendOptions.WasmSplit"/>).</param>
    /// <returns>The arguments, starting with <c>build</c>.</returns>
    /// <exception cref="ArgumentException">The name breaks a rule.</exception>
    public static IReadOnlyList<string> GetDxBuildArguments(FrontendConfiguration configuration, string applicationName, bool wasmSplit)
    {
        var application = string.Equals(applicationName, FrontendApplication.DefaultName, StringComparison.Ordinal)
            ? FrontendApplication.Default
            : FrontendApplication.FromName(applicationName);
        var arguments = configuration == FrontendConfiguration.Release
            ? new List<string> { "build", "--platform", "web", "--release", "--debug-symbols", "false" }
            : new List<string> { "build", "--platform", "web" };
        if (!application.IsDefault)
        {
            arguments.Add("--base-path");
            arguments.Add(application.BasePath);
        }

        if (wasmSplit)
        {
            arguments.Add("--wasm-split");
        }

        return arguments;
    }

    /// <summary>
    /// Checks everything a build needs on this machine - Rust and its WebAssembly target, the
    /// standalone Tailwind program from the Styling package beside the build program
    /// (<see cref="PackagedTailwind"/>; started once with <c>--help</c>), wasm-opt, the dx and
    /// wasm-bindgen programs, and the libgit2 library dx needs to start (from the CodeBrix.LibGit2 package; dx is started once, with
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

        var tailwindProblem = PackagedTailwind.TryLocate(_toolsBaseDirectory, HostTools.CurrentRuntimeIdentifier, out var tailwind);
        string tailwindVersion = null;
        string tailwindFailure = null;
        if (tailwind != null)
        {
            var probe = await _runner.RunAsync(tailwind.Path, new[] { "--help" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            tailwindVersion = probe.NotFound ? null : FrontendRequirements.ParseTailwindVersion(probe.Output);
            tailwindFailure = tailwindVersion == null ? OutputText.Tail(probe.Output, 5) : null;
        }

        items.Add(PrerequisiteRules.EvaluateTailwind(tailwind, tailwindProblem, tailwindVersion, tailwindFailure));

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

        return new PrerequisiteReport(PrerequisiteRules.Ordered(items), rustVersion, tailwindVersion, wasmOpt, tools, dxVersion, tailwindVersion == null ? null : tailwind);
    }

    /// <summary>
    /// Runs <see cref="CheckPrerequisitesAsync"/> and refuses, before anything is started, when
    /// something the step needs is missing - Rust (or its WebAssembly target, or a version older
    /// than the minimum), the packaged Tailwind, dx and wasm-bindgen, the libgit2 library, and
    /// wasm-opt when it will be run - with the check's own message for each.
    /// </summary>
    /// <param name="what">What is refused, as the message's subject.</param>
    /// <param name="needsWasmOpt">Whether the step runs wasm-opt.</param>
    /// <param name="cancellationToken">Stops the probes.</param>
    private async Task RequirePrerequisitesAsync(string what, bool needsWasmOpt, CancellationToken cancellationToken)
    {
        var report = await CheckPrerequisitesAsync(cancellationToken).ConfigureAwait(false);
        var blocking = PrerequisiteRules.Blocking(report.Items, needsWasmOpt);
        if (blocking.Count > 0)
        {
            throw new FrontendBuildException(PrerequisiteRules.Refusal(what, blocking));
        }
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
    /// <c>src/generated</c>, the shared Rust crate into <c>platform/codebrix-webplatform</c>,
    /// the crates into <see cref="PlatformFrontendOptions.ResolvedVendorDirectory"/> (for
    /// <see cref="CrateMode.PlatformCratesOnly"/>; unpacked only when an archive changed), and
    /// the cargo configuration into <c>.cargo/config.toml</c>. A frontend without a
    /// <c>Cargo.lock</c> gets the platform's as its starting point. With several applications
    /// (<see cref="PlatformFrontendOptions.Applications"/>) the backend is built and mapped once
    /// and every application's frontend folder gets the same generated code and shared crate;
    /// they share the one vendor folder. Files that already have the right content are not
    /// touched, so cargo does not rebuild for nothing. Last, when a frontend uses Tailwind and
    /// its generated stylesheet is missing, the packaged Tailwind program (<see cref="PackagedTailwind"/>)
    /// writes it once (see <see cref="PlatformFrontendOptions.GenerateMissingStylesheet"/>); a
    /// missing packaged program is reported in the result, never thrown.
    /// <para>
    /// The crates are the platform's own plus those of every registered crate bundle
    /// (<see cref="CrateBundles"/>). One copy of a crate is written however many bundles carry
    /// it. The rule is strict: a crate of the same name and version must have the same content
    /// wherever it comes from, and a bundle must not bring a version of a crate that the
    /// platform or another bundle carries in a different version; otherwise nothing is written
    /// and the exception names each such crate, the versions and the packages. For
    /// <see cref="CrateMode.ApplicationAddsCrates"/> the same check is made and nothing is
    /// unpacked (cargo fetches the bundles' crates like any other).
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Stops the backend build.</param>
    /// <returns>The map of the backend and the files that changed, for the first application;
    /// <see cref="MaterializeResult.Applications"/> has one result per application.</returns>
    /// <exception cref="FrontendBuildException">A frontend folder or the backend is missing,
    /// the backend does not build, its contract is not fully declared (the message lists every
    /// problem; the inner exception is BackendMapper's), an output folder or the cargo
    /// configuration holds files this library did not write, the platform's crates are not
    /// beside the build program, or the registered crate bundles break the strict rule.</exception>
    public async Task<MaterializeResult> MaterializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plan in _plans)
        {
            RequireFrontend(plan);
        }

        var assembly = await PrepareBackendAsync(cancellationToken).ConfigureAwait(false);

        BackendMap map;
        IReadOnlyList<GeneratedFile> files;
        try
        {
            map = CodeBrix.WebPlatform.BackendMapper.BackendMapper.Map(new BackendMapOptions { AssemblyPath = assembly, Types = Options.Types });
            files = RustClientGenerator.Generate(map, Options.ClientOptions);
            _runner.Say($"  mapped {map.Controllers.Sum(c => c.Endpoints.Count)} endpoints in {map.Controllers.Count} controllers, {map.Hubs.Count} hubs, {map.Structs.Count} contract classes and {map.Enums.Count} enums.");
        }
        catch (BackendMapException exception)
        {
            throw new FrontendBuildException("The client code could not be generated from the backend. " + exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new FrontendBuildException(exception.Message, exception);
        }

        var (vendorChanged, bundles) = MaterializeVendor();
        var vendored = Options.CrateMode == CrateMode.PlatformCratesOnly ? PlatformCrates.ReadVendoredCrates(Options.ResolvedVendorDirectory) : null;
        var results = new List<MaterializeResult>();
        foreach (var plan in _plans)
        {
            IReadOnlyList<string> generated;
            IReadOnlyList<string> shared;
            try
            {
                generated = RustClientGenerator.WriteTo(plan.GeneratedClientDirectory, files);
                shared = SharedRustCrate.ExtractTo(plan.SharedCrateDirectory);
            }
            catch (InvalidOperationException exception)
            {
                throw new FrontendBuildException(exception.Message, exception);
            }

            var (configurationWritten, lockWritten) = MaterializeCargoFiles(plan);
            var lockFile = await KeepLockFileInStepAsync(plan, lockWritten ? null : vendored, cancellationToken).ConfigureAwait(false);
            var (stylesheet, stylesheetMessage) = await EnsureStylesheetAsync(plan, cancellationToken).ConfigureAwait(false);
            var result = new MaterializeResult(map, generated, shared, vendorChanged, configurationWritten, lockWritten, stylesheet, stylesheetMessage)
            {
                ApplicationName = plan.Application.Name,
                CrateBundles = bundles,
                LockFileMarked = lockFile.Marked,
                LockFileRepairs = lockFile.Repairs,
                StaleLockFileEntries = lockFile.Stale,
                LockFileUpdateCommand = lockFile.Command,
                LockFileWarning = lockFile.Warning,
            };
            results.Add(result);
            var subject = plan.Label.Length == 0 ? string.Empty : "application" + plan.Label + ": ";
            _runner.Say(result.WasUpToDate
                ? $"  {subject}the generated client code, the shared crate and the crates were already up to date."
                : $"  {subject}wrote {generated.Count} generated file(s) and {shared.Count} shared-crate file(s)"
                    + (vendorChanged ? (Options.CrateMode == CrateMode.PlatformCratesOnly ? ", the crates" : ", removed the platform's vendor folder") : string.Empty)
                    + (configurationWritten ? ", the cargo configuration" : string.Empty)
                    + (lockWritten ? ", a starting Cargo.lock" : string.Empty)
                    + (lockFile.Repairs.Count > 0 ? ", Cargo.lock brought in step (" + string.Join(", ", lockFile.Repairs) + ")" : string.Empty)
                    + (stylesheet == StylesheetOutcome.Generated ? ", the Tailwind stylesheet" : string.Empty)
                    + ".");
            if (result.StylesheetMissing)
            {
                _runner.Say("  " + stylesheetMessage);
            }

            if (lockFile.Warning.Length > 0)
            {
                _runner.Say("  WARNING: " + lockFile.Warning);
            }
        }

        foreach (var result in results)
        {
            result.Applications = results;
        }

        return results[0];
    }

    /// <summary>
    /// Writes the generated Tailwind stylesheet of the first application when it uses Tailwind
    /// and the file is missing (see <see cref="EnsureStylesheetAsync(ApplicationPlan, CancellationToken)"/>).
    /// </summary>
    internal Task<(StylesheetOutcome Outcome, string Message)> EnsureStylesheetAsync(CancellationToken cancellationToken)
        => EnsureStylesheetAsync(_plans[0], cancellationToken);

    /// <summary>
    /// Writes the generated Tailwind stylesheet when the frontend uses Tailwind and the file is
    /// missing, by starting the packaged Tailwind program once in the frontend folder. Never
    /// throws for a missing or failing Tailwind: the outcome says what happened.
    /// </summary>
    internal async Task<(StylesheetOutcome Outcome, string Message)> EnsureStylesheetAsync(ApplicationPlan plan, CancellationToken cancellationToken)
    {
        var input = plan.TailwindInputFile;
        var output = plan.TailwindOutputFile;
        var decided = StylesheetRules.Decide(Options.GenerateMissingStylesheet, File.Exists(input), File.Exists(output));
        if (decided != null)
        {
            return (decided.Value, StylesheetRules.Describe(decided.Value, input, output));
        }

        var problem = PackagedTailwind.TryLocate(_toolsBaseDirectory, HostTools.CurrentRuntimeIdentifier, out var tailwind);
        if (problem != null)
        {
            return (StylesheetOutcome.SkippedTailwindNotAvailable, StylesheetRules.NotGenerated(output, problem));
        }

        _runner.Say($"  generating the missing Tailwind stylesheet{(plan.Label.Length == 0 ? string.Empty : " of" + plan.Label)} ...");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var run = await _runner.RunAsync(tailwind.Path, new[] { "-i", input, "-o", output }, plan.FrontendDirectory, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded || !File.Exists(output))
        {
            var reason = "the Tailwind program failed:" + Environment.NewLine + OutputText.Tail(run.Output, 20);
            return (StylesheetOutcome.SkippedTailwindFailed, StylesheetRules.NotGenerated(output, reason));
        }

        return (StylesheetOutcome.Generated, StylesheetRules.Describe(StylesheetOutcome.Generated, input, output));
    }

    /// <summary>
    /// Checks the registered crate bundles against the platform's crates and, for
    /// <see cref="CrateMode.PlatformCratesOnly"/>, makes the one vendor folder hold all of them
    /// (for <see cref="CrateMode.ApplicationAddsCrates"/> a vendor folder this library wrote is
    /// removed).
    /// </summary>
    private (bool VendorChanged, IReadOnlyList<CrateBundleOutcome> Bundles) MaterializeVendor()
    {
        var vendor = Options.ResolvedVendorDirectory;
        var registered = CrateBundlesOverride ?? CrateBundles.Registered;
        var archive = PlatformCrates.GetArchivePath(_toolsBaseDirectory);
        if (Options.CrateMode != CrateMode.PlatformCratesOnly)
        {
            var bundles = registered.Count == 0 ? Array.Empty<CrateBundleOutcome>() : CrateBundleMerge.Check(archive, registered).Outcomes;
            return (PlatformCrates.Remove(vendor), bundles);
        }

        var merge = CrateBundleMerge.Check(archive, registered);
        if (!File.Exists(Path.Combine(vendor, PlatformCrates.StampFileName)))
        {
            _runner.Say(registered.Count == 0
                ? "  writing the platform's crates into the frontend ..."
                : "  writing the platform's crates and the registered crate bundles into the frontend ...");
        }

        return (PlatformCrates.ExtractTo(merge, vendor), merge.Outcomes);
    }

    /// <summary>The cargo configuration of one frontend, and a starting Cargo.lock when it has none.</summary>
    private (bool ConfigurationWritten, bool LockWritten) MaterializeCargoFiles(ApplicationPlan plan)
    {
        var configuration = CargoConfiguration.Create(Options.CrateMode, CargoConfiguration.VendorPath(plan.FrontendDirectory, Options.ResolvedVendorDirectory));
        var configurationWritten = CargoConfiguration.Write(plan.CargoConfigurationFile, configuration);

        var lockFile = Path.Combine(plan.FrontendDirectory, LockFileRules.FileName);
        var lockWritten = false;
        if (!File.Exists(lockFile))
        {
            File.WriteAllText(lockFile, LockFileRules.AddMarker(PlatformCrates.LockFileText), new UTF8Encoding(false));
            lockWritten = true;
        }

        return (configurationWritten, lockWritten);
    }

    /// <summary>
    /// Compares a frontend's Cargo.lock with the vendor folder (<see cref="CrateMode.PlatformCratesOnly"/>
    /// only; nothing is done in the other mode, where cargo takes crates from crates.io, which
    /// keeps every version a lock file names). Entries that name a version of a crate the folder
    /// holds only in other versions are STALE. A lock file with the marker line has exactly those
    /// entries updated by cargo (<c>cargo update --offline -p ...</c> in the frontend folder, after
    /// the vendor folder and the cargo configuration are written); one without the marker is
    /// never changed, and the outcome carries the warning with the command to run. A starting
    /// lock file written in the same run is not compared (<paramref name="vendored"/> is null):
    /// it is the platform's own, in step with the platform's crates, and the strict rule keeps
    /// every bundle from bringing another version of one of them.
    /// </summary>
    /// <exception cref="FrontendBuildException">The update failed; the file is as it was.</exception>
    private async Task<LockFileOutcome> KeepLockFileInStepAsync(ApplicationPlan plan, IReadOnlyDictionary<string, HashSet<string>> vendored, CancellationToken cancellationToken)
    {
        var path = Path.Combine(plan.FrontendDirectory, LockFileRules.FileName);
        if (!File.Exists(path))
        {
            return LockFileOutcome.None;
        }

        var original = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(original);
        var marked = LockFileRules.HasMarker(text);
        if (vendored == null)
        {
            return new LockFileOutcome(marked);
        }

        var locked = LockFileRules.ReadCratesIoPackages(text);
        var stale = LockFileRules.FindStale(locked, vendored);
        if (stale.Count == 0)
        {
            return new LockFileOutcome(marked);
        }

        var command = LockFileRules.UpdateCommand(stale, locked);
        if (!marked)
        {
            return new LockFileOutcome(false)
            {
                Stale = stale.Select(c => c.ToString()).ToList(),
                Command = command,
                Warning = LockFileRules.StaleMessage(plan.FrontendDirectory, stale, locked, vendored, marked: false),
            };
        }

        _runner.Say($"  bringing the Cargo.lock of '{plan.FrontendDirectory}' in step with the crates: {command} ...");
        var run = await _runner.RunAsync("cargo", LockFileRules.UpdateArguments(stale, locked), plan.FrontendDirectory, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        string problem = null;
        IReadOnlyList<LockFileRepair> repairs = Array.Empty<LockFileRepair>();
        if (run.NotFound)
        {
            problem = "cargo was not found on PATH. Install Rust (CheckPrerequisitesAsync says how) and materialize again.";
        }
        else if (!run.Succeeded)
        {
            problem = $"`{command}` failed in that folder:" + Environment.NewLine + OutputText.Tail(run.Output, 12) + Environment.NewLine
                + "Most often the frontend's Cargo.toml asks for a version the vendor folder does not hold (an exact \"=x.y.z\" requirement, or one the platform has moved past); change it to a version the folder holds and materialize again.";
        }
        else
        {
            var updated = File.ReadAllText(path, Encoding.UTF8);
            if (!LockFileRules.HasMarker(updated) && LockFileRules.HasCargoHeader(updated))
            {
                // cargo keeps a comment on the third line; should a later cargo drop it, it is put back.
                updated = LockFileRules.AddMarker(updated);
                File.WriteAllText(path, updated, new UTF8Encoding(false));
            }

            var after = LockFileRules.ReadCratesIoPackages(updated);
            var left = LockFileRules.FindStale(after, vendored);
            if (left.Count > 0 || !LockFileRules.HasMarker(updated))
            {
                problem = left.Count > 0
                    ? $"`{command}` left entries the vendor folder does not hold: {string.Join(", ", left)}."
                    : "cargo removed the marker line and it could not be put back.";
            }
            else
            {
                repairs = LockFileRules.DescribeRepairs(stale, locked, after);
            }
        }

        if (problem != null)
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
            {
                File.WriteAllBytes(path, original);
            }

            throw new FrontendBuildException(
                $"The {LockFileRules.FileName} of the frontend '{plan.FrontendDirectory}' names versions of crates that the vendor folder no longer holds ({LockFileRules.Describe(stale, vendored)}), and bringing it in step failed, so the file was left as it was. "
                + problem + Environment.NewLine
                + $"To manage the file yourself instead, remove its marker line (the line that starts \"{PlatformCrates.LockFileMarkerPrefix}\").");
        }

        return new LockFileOutcome(true) { Repairs = repairs };
    }

    /// <summary>
    /// Refuses to start dx while a frontend's Cargo.lock names a version of a crate the vendor
    /// folder holds only in other versions: the offline build would fail with cargo's message,
    /// and this one says what to run (<see cref="CrateMode.PlatformCratesOnly"/> only).
    /// </summary>
    /// <exception cref="FrontendBuildException">A lock file has stale entries.</exception>
    private void RequireLockFilesInStep(IEnumerable<ApplicationPlan> plans)
    {
        if (Options.CrateMode != CrateMode.PlatformCratesOnly || !File.Exists(Path.Combine(Options.ResolvedVendorDirectory, PlatformCrates.StampFileName)))
        {
            return;
        }

        IReadOnlyDictionary<string, HashSet<string>> vendored = null;
        foreach (var plan in plans)
        {
            var path = Path.Combine(plan.FrontendDirectory, LockFileRules.FileName);
            if (!File.Exists(path))
            {
                continue;
            }

            vendored ??= PlatformCrates.ReadVendoredCrates(Options.ResolvedVendorDirectory);
            var text = File.ReadAllText(path, Encoding.UTF8);
            var locked = LockFileRules.ReadCratesIoPackages(text);
            var stale = LockFileRules.FindStale(locked, vendored);
            if (stale.Count > 0)
            {
                throw new FrontendBuildException(LockFileRules.StaleMessage(plan.FrontendDirectory, stale, locked, vendored, LockFileRules.HasMarker(text)) + Environment.NewLine + "dx was not started.");
            }
        }
    }

    /// <summary>
    /// Builds the servable site of every application with dx, each from a clean dx output
    /// folder. A release build is shrunk by wasm-opt, which dx runs - the packaged one from
    /// CodeBrix.WebPlatform.WasmShrink first on PATH, an installed one as the fallback - and
    /// fails if dx reports that wasm-opt failed (dx itself only logs that). A debug build is not
    /// shrunk. <see cref="PlatformFrontendOptions.Shrink"/> changes either: <c>true</c> shrinks a
    /// debug build (this library runs wasm-opt on dx's output), <c>false</c> leaves a release
    /// build unshrunk (dx is told to skip wasm-opt, and no wasm-opt is needed). An application
    /// built with bundle splitting (<see cref="PlatformFrontendOptions.WasmSplit"/>) is always
    /// shrunk by dx, piece by piece. An application that has a name is built with that name as
    /// its base path. <see cref="FrontendBuildResult.Shrunk"/> says what happened. dx is started
    /// with the libgit2 library it needs (see <see cref="Libgit2Directory"/>). Call
    /// <see cref="MaterializeAsync"/> first.
    /// <para>
    /// With a web root, the sites are copied into it once every application has been built: the
    /// application at the site root into the web root itself, an application that has a name
    /// into the sub-folder of that name, and the frontend manifest
    /// (<see cref="FrontendManifest.FileName"/>) beside them for the backend to read. When there
    /// is an application at the site root, everything the web root held is replaced (a
    /// <c>.gitkeep</c> file is kept). When every application has a name, only the applications'
    /// sub-folders and the manifest are replaced; other files in the web root are left alone.
    /// Then, as <see cref="PlatformFrontendOptions.CompressOutput"/> asks (Brotli and gzip by
    /// default), a <c>.br</c> and/or <c>.gz</c> copy is written beside each compressible file
    /// of the built applications (never beside the developer's own files);
    /// <see cref="FrontendBuildResult.Compression"/> says what was written. Nothing is
    /// compressed without a web root.
    /// </para>
    /// </summary>
    /// <param name="configuration">Release (the build that ships) or Debug.</param>
    /// <param name="webRootDirectory">The folder to copy the built sites into, or <c>null</c> to
    /// leave each site where dx put it.</param>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>Where the first application's site is, and how it was built;
    /// <see cref="FrontendBuildResult.Applications"/> has one result per application.</returns>
    /// <exception cref="FrontendBuildException">Something the build needs is missing on this machine
    /// - checked FIRST, before anything starts, with <see cref="CheckPrerequisitesAsync"/>: Rust
    /// at the minimum version with its WebAssembly target, the packaged Tailwind, dx and
    /// wasm-bindgen, the libgit2 library, and wasm-opt when the build shrinks (the message lists
    /// each with how to fix it); a frontend's Cargo.lock names a version of a crate the vendor
    /// folder no longer holds (the message gives the command that brings it in step; dx is not
    /// started), dx failed,
    /// wasm-opt failed, <see cref="PlatformFrontendOptions.CompressOutput"/> is not a
    /// <see cref="FrontendCompression"/> value, or a compressed copy could not be written.</exception>
    public async Task<FrontendBuildResult> BuildAsync(FrontendConfiguration configuration = FrontendConfiguration.Release, string webRootDirectory = null, CancellationToken cancellationToken = default)
    {
        var compression = Options.CompressOutput;
        if (!Enum.IsDefined(compression))
        {
            throw new FrontendBuildException(
                $"PlatformFrontendOptions.CompressOutput is {(int)compression}, which is not a FrontendCompression value. Set it to None, Brotli, Gzip or BrotliAndGzip (the default).");
        }

        foreach (var plan in _plans)
        {
            RequireFrontend(plan);
        }

        RequireLockFilesInStep(_plans);
        var needsWasmOpt = _plans.Any(p => ShrinkPlan.Decide(configuration, Options.Shrink, p.WasmSplit).NeedsWasmOpt);
        await RequirePrerequisitesAsync("The build", needsWasmOpt, cancellationToken).ConfigureAwait(false);
        var tools = FindHostTools();
        var built = new List<(ApplicationPlan Plan, string Output, bool Shrunk, WasmOptSource Source)>();
        foreach (var plan in _plans)
        {
            var (output, shrunk, source) = await BuildOneAsync(plan, configuration, tools, cancellationToken).ConfigureAwait(false);
            built.Add((plan, output, shrunk, source));
        }

        string webRoot = null;
        string manifest = null;
        IReadOnlyList<int> copied = null;
        var compressed = new FrontendCompressionResult[built.Count];
        if (webRootDirectory != null)
        {
            webRoot = Path.GetFullPath(webRootDirectory);
            copied = WebRootWriter.Write(webRoot, built.Select(b => (b.Plan.Application, b.Output)).ToList());
            manifest = FrontendManifestWriter.GetPath(webRoot);
            for (var i = 0; i < built.Count; i++)
            {
                compressed[i] = CompressWebRootFiles(built[i].Plan, built[i].Output, ApplicationWebRoot(webRoot, built[i].Plan), compression, cancellationToken);
            }
        }

        var results = new List<FrontendBuildResult>();
        for (var i = 0; i < built.Count; i++)
        {
            var (plan, output, shrunk, source) = built[i];
            results.Add(new FrontendBuildResult(configuration, output, shrunk, shrunk ? source : WasmOptSource.Missing, tools, webRoot, copied == null ? 0 : copied[i])
            {
                ApplicationName = plan.Application.Name,
                WasmSplit = plan.WasmSplit,
                WebRootApplicationDirectory = webRoot == null ? null : ApplicationWebRoot(webRoot, plan),
                ManifestFile = manifest,
                Compression = compressed[i],
            });
        }

        foreach (var result in results)
        {
            result.Applications = results;
        }

        return results[0];
    }

    private static string ApplicationWebRoot(string webRoot, ApplicationPlan plan)
        => plan.Application.IsDefault ? webRoot : Path.Combine(webRoot, plan.Application.Folder);

    /// <summary>
    /// Writes the compressed copies of one application's files in the web root (the files dx
    /// built, nothing else) and says what it did.
    /// </summary>
    private FrontendCompressionResult CompressWebRootFiles(ApplicationPlan plan, string site, string applicationWebRoot, FrontendCompression compression, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(site, f)).ToList();
        var result = OutputCompressor.Compress(applicationWebRoot, files, compression, cancellationToken);
        var what = "the frontend" + (plan.Label.Length == 0 ? string.Empty : " application" + plan.Label);
        if (compression != FrontendCompression.None)
        {
            _runner.Say($"Compressed {result.Files.Count} files of {what} in the web root ({compression}) in {result.Elapsed.TotalSeconds:0.0} s: {result.OriginalBytes} bytes"
                + (result.BrotliFileCount > 0 ? $", {result.BrotliBytes} with Brotli" : string.Empty)
                + (result.GzipFileCount > 0 ? $", {result.GzipBytes} with gzip" : string.Empty) + ".");
        }

        return result;
    }

    /// <summary>Builds one application's site with dx and returns where dx left it.</summary>
    private async Task<(string Output, bool Shrunk, WasmOptSource Source)> BuildOneAsync(ApplicationPlan plan, FrontendConfiguration configuration, HostTools tools, CancellationToken cancellationToken)
    {
        var shrinkPlan = ShrinkPlan.Decide(configuration, Options.Shrink, plan.WasmSplit);
        var frontend = plan.FrontendDirectory;
        var environment = CreateDxEnvironment(tools, plan);
        environment[ShrinkPlan.SkipVariable] = shrinkPlan.SkipVariableValue;
        var source = shrinkPlan.NeedsWasmOpt || shrinkPlan.SkipRequested ? await FindWasmOptAsync(cancellationToken).ConfigureAwait(false) : WasmOptSource.Missing;
        if (shrinkPlan.NeedsWasmOpt && source == WasmOptSource.Missing)
        {
            throw new FrontendBuildException(PrerequisiteRules.DescribeWasmOpt(source, HostTools.CurrentRuntimeIdentifier).Message);
        }

        var dxOutput = Path.Combine(frontend, "target", "dx");
        if (Directory.Exists(dxOutput))
        {
            Directory.Delete(dxOutput, recursive: true);
        }

        var what = "the frontend" + (plan.Label.Length == 0 ? string.Empty : " application" + plan.Label);
        var splitting = plan.WasmSplit ? ", bundle splitting" : string.Empty;
        _runner.Say(configuration == FrontendConfiguration.Release
            ? (shrinkPlan.SkipRequested ? $"Building {what}, unshrunk (cargo, wasm-bindgen, Tailwind; Shrink is false) ..." : $"Building {what} (cargo, wasm-bindgen{splitting}, wasm-opt, Tailwind) ...")
            : (plan.WasmSplit ? $"Building {what} for development (cargo, wasm-bindgen{splitting}, wasm-opt, Tailwind) ..." : $"Building {what} for development (cargo, wasm-bindgen, Tailwind) ..."));
        var arguments = GetDxBuildArguments(configuration, plan.Application.Name, plan.WasmSplit);
        var result = await _runner.RunAsync(tools.DxPath, arguments, frontend, environment, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var output = GetOutputDirectory(plan, configuration);
        if (!result.Succeeded || !File.Exists(Path.Combine(output, "index.html")))
        {
            throw new FrontendBuildException($"The build of {what} failed:" + Environment.NewLine + OutputText.Tail(result.Output, 60));
        }

        var dxShrank = shrinkPlan.DxRanWasmOpt(configuration, result.Output);
        if (dxShrank && ReportsShrinkFailure(result.Output))
        {
            throw new FrontendBuildException($"dx built {what}, but wasm-opt failed and the WebAssembly was left unshrunk:" + Environment.NewLine + OutputText.Tail(result.Output, 60));
        }

        if (shrinkPlan.LibraryShrinks)
        {
            await ShrinkFilesAsync(output, source, cancellationToken).ConfigureAwait(false);
        }

        return (output, dxShrank || shrinkPlan.LibraryShrinks, source);
    }

    /// <summary>
    /// Runs <c>dx serve</c> with hot reload for the application that has no name - or, when
    /// there is none, for the only application. See <see cref="ServeAsync(string, CancellationToken)"/>.
    /// </summary>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>dx's exit code.</returns>
    /// <exception cref="FrontendBuildException">Several applications have names and none is
    /// without one (the message lists them), or the frontend folder, a program or the libgit2
    /// library is missing.</exception>
    public Task<int> ServeAsync(CancellationToken cancellationToken = default) => ServeAsync(null, cancellationToken);

    /// <summary>
    /// Runs <c>dx serve</c> with hot reload in one application's frontend folder, with the same
    /// environment as <see cref="BuildAsync"/> (the libgit2 library included), the application's
    /// base path when it has a name, and its serve features, until it ends or is cancelled. Its
    /// output goes to <see cref="PlatformFrontendOptions.Output"/>. Nothing is compressed
    /// (<see cref="PlatformFrontendOptions.CompressOutput"/> is for <see cref="BuildAsync"/>
    /// into a web root only). Call <see cref="MaterializeAsync"/> first.
    /// </summary>
    /// <param name="applicationName">The application to run. <c>null</c> or empty: the
    /// application that has no name; when there is none and there is exactly one application,
    /// that one.</param>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>dx's exit code.</returns>
    /// <exception cref="FrontendBuildException">There is no application of that name, or no name
    /// was given and it cannot be told which is meant (the message lists the names); the
    /// frontend folder is missing; something dx serve needs is missing on this machine - checked
    /// before dx is started, with <see cref="CheckPrerequisitesAsync"/>: Rust, the packaged
    /// Tailwind, dx and wasm-bindgen, the libgit2 library (the message lists each with how to fix
    /// it); or the frontend's Cargo.lock names a version of a crate the vendor folder no longer holds (the message gives the
    /// command that brings it in step).</exception>
    public async Task<int> ServeAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var plan = FindPlan(applicationName, forServe: true);
        var frontend = RequireFrontend(plan);
        RequireLockFilesInStep(new[] { plan });
        await RequirePrerequisitesAsync("dx serve", false, cancellationToken).ConfigureAwait(false);
        var tools = FindHostTools();
        var arguments = new List<string> { "serve", "--platform", "web" };
        if (!plan.Application.IsDefault)
        {
            arguments.Add("--base-path");
            arguments.Add(plan.Application.BasePath);
        }

        if (plan.ServeFeatures.Count > 0)
        {
            arguments.Add("--features");
            arguments.Add(string.Join(",", plan.ServeFeatures));
        }

        _runner.Say($"Starting dx serve{(plan.Label.Length == 0 ? string.Empty : " for" + plan.Label)} (Ctrl+C stops it) ...");
        var result = await _runner.RunAsync(tools.DxPath, arguments, frontend, CreateDxEnvironment(tools, plan), cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    /// <summary>
    /// Where dx leaves the first application's site for a configuration:
    /// <c>target/dx/&lt;package name&gt;/&lt;release|debug&gt;/web/public</c> in the frontend folder.
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <returns>The folder, as a full path.</returns>
    /// <exception cref="FrontendBuildException">The frontend's Cargo.toml is missing or names no package.</exception>
    public string GetOutputDirectory(FrontendConfiguration configuration) => GetOutputDirectory(_plans[0], configuration);

    /// <summary>
    /// Where dx leaves an application's site for a configuration:
    /// <c>target/dx/&lt;package name&gt;/&lt;release|debug&gt;/web/public</c> in its frontend folder.
    /// </summary>
    /// <param name="applicationName">The application's name; <c>null</c> or empty for the one without a name.</param>
    /// <param name="configuration">The kind of build.</param>
    /// <returns>The folder, as a full path.</returns>
    /// <exception cref="FrontendBuildException">There is no application of that name, or its
    /// Cargo.toml is missing or names no package.</exception>
    public string GetOutputDirectory(string applicationName, FrontendConfiguration configuration)
        => GetOutputDirectory(FindPlan(applicationName, forServe: false), configuration);

    private string GetOutputDirectory(ApplicationPlan plan, FrontendConfiguration configuration)
    {
        var frontend = RequireFrontend(plan);
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
    /// Finds an application's plan by name. No name: the application that has none; for
    /// <paramref name="forServe"/>, when there is none and there is exactly one application,
    /// that one.
    /// </summary>
    private ApplicationPlan FindPlan(string applicationName, bool forServe)
    {
        var names = string.Join(", ", _plans.Select(p => p.Application.IsDefault ? "(no name)" : "'" + p.Application.Name + "'"));
        if (!string.IsNullOrEmpty(applicationName) && !string.Equals(applicationName, FrontendApplication.DefaultName, StringComparison.Ordinal))
        {
            return _plans.FirstOrDefault(p => !p.Application.IsDefault && string.Equals(p.Application.Name, applicationName, StringComparison.Ordinal))
                ?? throw new FrontendBuildException($"There is no frontend application named '{applicationName}'. The applications are: {names}.");
        }

        var unnamed = _plans.FirstOrDefault(p => p.Application.IsDefault);
        if (unnamed != null)
        {
            return unnamed;
        }

        if (forServe && string.IsNullOrEmpty(applicationName) && _plans.Count == 1)
        {
            return _plans[0];
        }

        throw new FrontendBuildException(string.IsNullOrEmpty(applicationName) && forServe
            ? $"Name the frontend application to run: every application has a name, and there are several ({names})."
            : $"There is no frontend application without a name. The applications are: {names}.");
    }

    /// <summary>
    /// The environment dx runs with. PATH starts with the folder of the packaged Tailwind program
    /// (<see cref="PackagedTailwind"/>), then the folder of the packaged wasm-opt (when the package
    /// has one for this machine) and then the folder of wasm-bindgen, so those are the ones dx
    /// finds - a Tailwind installed on the machine is never the one dx runs; DX_HOME is
    /// <see cref="DxHomeDirectory"/>; and the folder holding the libgit2 library dx needs to start (<see cref="Libgit2Directory"/>) goes first on the
    /// loader's search variable - LD_LIBRARY_PATH on Linux, DYLD_LIBRARY_PATH on macOS, PATH on
    /// Windows. Only dx's process gets these.
    /// </summary>
    /// <exception cref="FrontendBuildException">The packaged Tailwind program was not found, or
    /// libgit2 could not be supplied.</exception>
    internal Dictionary<string, string> CreateDxEnvironment(HostTools tools, ApplicationPlan plan = null)
    {
        plan ??= _plans[0];
        var tailwindProblem = PackagedTailwind.TryLocate(_toolsBaseDirectory, tools.RuntimeIdentifier ?? HostTools.CurrentRuntimeIdentifier, out var tailwind);
        if (tailwindProblem != null)
        {
            throw new FrontendBuildException(tailwindProblem);
        }

        var path = tools.WasmBindgenDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        if (WasmShrinkTool.IsAvailable)
        {
            path = WasmShrinkTool.GetToolDirectory() + Path.PathSeparator + path;
        }

        path = tailwind.Directory + Path.PathSeparator + path;

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = path,
            ["DX_HOME"] = plan.DxHomeDirectory,
        };
        var problem = PrepareLibgit2(tools, out var supply, plan);
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
    internal string PrepareLibgit2(HostTools tools, out Libgit2Supply supply, ApplicationPlan plan = null)
    {
        var version = GlobalSettings.Version;
        var packageVersion = version.InformationalVersion ?? string.Empty;
        var plus = packageVersion.IndexOf('+');
        return Libgit2Supply.Prepare(
            tools.DxPath,
            tools.RuntimeIdentifier ?? HostTools.CurrentRuntimeIdentifier,
            _toolsBaseDirectory,
            Path.Combine((plan ?? _plans[0]).DxHomeDirectory, "libgit2"),
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

    private static string RequireFrontend(ApplicationPlan plan)
    {
        var frontend = plan.FrontendDirectory;
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
