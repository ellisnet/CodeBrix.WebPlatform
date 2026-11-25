using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using WorkSite.BuildFrontend.Steps;

namespace WorkSite.BuildFrontend;

/// <summary>
/// The commands of WorkSite.BuildFrontend. The generic frontend work - the prerequisite check, writing
/// the generated client code, the shared crate, the platform's crates and the cargo
/// configuration into the frontend, running dx, the platform's notices - is done by
/// CodeBrix.WebPlatform.FrontendBuilder's <see cref="PlatformFrontend"/>, with WorkSite's
/// settings from <see cref="WorkSiteFrontend"/>; WorkSite's own steps are in <see cref="Steps"/>.
/// </summary>
public sealed class Builder
{
    private readonly ToolRunner _runner;
    private readonly BuilderPaths _paths;
    private readonly Prerequisites _prerequisites;
    private readonly DevCertificateStep _certificate;
    private readonly ContainerStep _containers;
    private PlatformFrontend _frontend;

    /// <summary>
    /// Creates the builder for a WorkSite folder.
    /// </summary>
    /// <param name="layout">The WorkSite folders.</param>
    /// <param name="output">Where progress and program output go.</param>
    public Builder(WorkSiteLayout layout, TextWriter output)
    {
        _runner = new ToolRunner(output);
        _paths = new BuilderPaths(layout);
        _prerequisites = new Prerequisites(_runner, _paths);
        _certificate = new DevCertificateStep(_runner);
        _containers = new ContainerStep(_runner, _paths);
    }

    /// <summary>The paths this builder works with.</summary>
    public BuilderPaths Paths => _paths;

    /// <summary>
    /// The platform's frontend builder with WorkSite's settings. Created on first use.
    /// </summary>
    public PlatformFrontend Frontend
        => _frontend ??= new PlatformFrontend(WorkSiteFrontend.CreateOptions(_paths, _runner.Output));

    /// <summary>
    /// <c>check</c>: reports whether everything needed to build and run is installed, and where
    /// dx, wasm-bindgen, Tailwind, wasm-opt and the libgit2 library dx needs come from (the
    /// packages).
    /// </summary>
    /// <param name="cancellationToken">Stops the probes.</param>
    /// <returns>True when nothing required is missing.</returns>
    public async Task<bool> CheckAsync(CancellationToken cancellationToken = default)
    {
        var report = await Frontend.CheckPrerequisitesAsync(cancellationToken).ConfigureAwait(false);
        var problems = new List<string>(report.Problems);
        foreach (var item in report.Items)
        {
            _runner.Say("  " + item);
        }

        var containers = await _prerequisites.CheckContainersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var problem in containers)
        {
            _runner.Say("  MISSING  " + problem);
        }

        problems.AddRange(containers);
        _runner.Say(problems.Count == 0 ? "Everything required is installed." : $"{problems.Count} required item(s) missing.");
        return problems.Count == 0;
    }

    /// <summary>
    /// <c>generate</c>: builds the backend, generates the Rust client code for its REST endpoints
    /// and hubs into the frontend's <c>src/generated</c> folder, and writes the platform's shared
    /// crate and crates into the frontend's <c>platform</c> folder and the cargo configuration
    /// into its <c>.cargo</c> folder (the platform's Materialize). <c>build</c> and <c>serve</c>
    /// do this first; run it alone to make the frontend compile with plain cargo, offline.
    /// </summary>
    /// <param name="cancellationToken">Stops the backend build.</param>
    /// <exception cref="FrontendBuildException">The backend does not build, or its contract is not
    /// fully declared.</exception>
    public Task GenerateAsync(CancellationToken cancellationToken = default) => Frontend.MaterializeAsync(cancellationToken);

    /// <summary>
    /// <c>build</c>: generates the client code from the backend (see <c>generate</c>), builds the
    /// frontend with the packaged dx into the backend project's frontend-built folder, and writes
    /// the notices beside it: WorkSite's own, followed by the platform's.
    /// </summary>
    /// <param name="cancellationToken">Stops the build.</param>
    /// <exception cref="BuilderException">A prerequisite is missing, or a file is.</exception>
    /// <exception cref="FrontendBuildException">A platform step failed.</exception>
    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        Require((await Frontend.CheckPrerequisitesAsync(cancellationToken).ConfigureAwait(false)).Problems);
        await GenerateAsync(cancellationToken).ConfigureAwait(false);
        if (!File.Exists(_paths.Layout.NoticesFile))
        {
            throw new BuilderException($"{_paths.Layout.NoticesFile} does not exist; it is WorkSite's own notices file and is part of the sample.");
        }

        var built = await Frontend.BuildAsync(FrontendConfiguration.Release, _paths.ApiProjectDirectory, cancellationToken).ConfigureAwait(false);
        // After every build (a full build removes the files it did not write itself); written
        // with its compressed copies, as the build writes its own files.
        Frontend.WriteBuiltFile(_paths.ApiProjectDirectory, BuilderPaths.BuiltNoticesFileName,
            CombineNotices(File.ReadAllText(_paths.Layout.NoticesFile), PlatformCrates.ReadNotices()), FrontendConfiguration.Release, cancellationToken);
        _runner.Say($"The frontend is built: {built.BuiltFileCount + 1} files in {built.BuiltFrontendDirectory}");
    }

    /// <summary>
    /// The notices file the site serves: WorkSite's own notices, then the platform's (everything
    /// the platform compiles into the WebAssembly file).
    /// </summary>
    /// <param name="workSiteNotices">WorkSite's own notices.</param>
    /// <param name="platformNotices">The platform's notices (<see cref="PlatformCrates.ReadNotices"/>).</param>
    public static string CombineNotices(string workSiteNotices, string platformNotices)
        => (workSiteNotices ?? string.Empty).TrimEnd() + "\n\n\n" + (platformNotices ?? string.Empty);

    /// <summary>
    /// <c>serve</c>: runs <c>dx serve</c> with hot reload for frontend development. The REST
    /// calls need a backend; the frontend's Dioxus.toml can proxy <c>/api</c> to the containers.
    /// </summary>
    /// <param name="cancellationToken">Stops dx.</param>
    /// <returns>dx's exit code.</returns>
    public async Task<int> ServeAsync(CancellationToken cancellationToken = default)
    {
        Require((await Frontend.CheckPrerequisitesAsync(cancellationToken).ConfigureAwait(false)).Problems);
        await GenerateAsync(cancellationToken).ConfigureAwait(false);
        return await Frontend.ServeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>env</c>: creates <c>.env</c> from <c>.env.example</c> with random secrets, when it does
    /// not exist yet. An existing file is never changed.
    /// </summary>
    /// <returns>True when the file was created.</returns>
    public bool CreateEnvFile()
    {
        if (File.Exists(_paths.EnvFile))
        {
            _runner.Say($"{_paths.EnvFile} already exists; it was not changed.");
            return false;
        }

        File.WriteAllText(_paths.EnvFile, EnvFile.CreateFromExample(File.ReadAllText(_paths.EnvExampleFile), BuilderPaths.DefaultCertificateFile));
        _runner.Say($"Created {_paths.EnvFile} with random secrets (git ignores this file). The demo account's password is in it.");
        return true;
    }

    /// <summary>
    /// <c>cert</c>: exports the HTTPS development certificate to the file named in <c>.env</c>.
    /// </summary>
    /// <param name="cancellationToken">Stops the export.</param>
    public async Task ExportCertificateAsync(CancellationToken cancellationToken = default)
    {
        var settings = LoadSettings(requireCertificateFile: false);
        await _certificate.ExportAsync(settings["WORKSITE_CERT_FILE"], settings["WORKSITE_CERT_PASSWORD"], cancellationToken).ConfigureAwait(false);
        var (_, subject, notAfter) = DevCertificateStep.Inspect(settings["WORKSITE_CERT_FILE"], settings["WORKSITE_CERT_PASSWORD"]);
        _runner.Say($"Exported the development certificate ({subject}, valid until {notAfter:yyyy-MM-dd}) to {settings["WORKSITE_CERT_FILE"]}");
        _runner.Say("Browsers trust it only after `dotnet dev-certs https --trust` has been run on this machine.");
    }

    /// <summary>
    /// <c>run</c>: builds everything, starts the two containers, waits for the site and prints
    /// its address. Creates <c>.env</c> and exports the certificate first when they are missing.
    /// </summary>
    /// <param name="skipBuild">Use what is already in the backend project's frontend-built
    /// folder instead of rebuilding the frontend.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The site's address.</returns>
    /// <exception cref="BuilderException">Something is missing or a step failed.</exception>
    public async Task<string> RunAsync(bool skipBuild = false, CancellationToken cancellationToken = default)
    {
        Require(await _prerequisites.CheckContainersAsync(cancellationToken).ConfigureAwait(false));
        CreateEnvFile();
        var settings = LoadSettings(requireCertificateFile: false);
        if (!File.Exists(settings["WORKSITE_CERT_FILE"]))
        {
            await _certificate.ExportAsync(settings["WORKSITE_CERT_FILE"], settings["WORKSITE_CERT_PASSWORD"], cancellationToken).ConfigureAwait(false);
            _runner.Say($"Exported the HTTPS development certificate to {settings["WORKSITE_CERT_FILE"]}");
        }

        var (thumbprint, subject, notAfter) = DevCertificateStep.Inspect(settings["WORKSITE_CERT_FILE"], settings["WORKSITE_CERT_PASSWORD"]);
        if (notAfter < DateTime.Now)
        {
            throw new BuilderException($"The HTTPS development certificate ({subject}) expired on {notAfter:yyyy-MM-dd}. Renew it with `dotnet dev-certs https --clean`, `dotnet dev-certs https --trust`, then `WorkSite.BuildFrontend cert`.");
        }

        if (!skipBuild)
        {
            await BuildAsync(cancellationToken).ConfigureAwait(false);
        }

        await _containers.PublishBackendAsync(cancellationToken).ConfigureAwait(false);
        await _containers.UpAsync(cancellationToken).ConfigureAwait(false);
        var port = int.TryParse(settings.GetValueOrDefault("WORKSITE_HTTPS_PORT"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 8443;
        _runner.Say("Waiting for the site to answer ...");
        if (!await ContainerStep.WaitUntilHealthyAsync(port, thumbprint, TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false))
        {
            throw new BuilderException("The containers started but the site did not answer. The web container's last log lines:" + Environment.NewLine
                + ToolRunner.Tail(await _containers.WebLogsAsync(cancellationToken: cancellationToken).ConfigureAwait(false), 40));
        }

        var address = $"https://localhost:{port}/";
        _runner.Say(string.Empty);
        _runner.Say($"WorkSite is running: {address}");
        if (string.Equals(settings.GetValueOrDefault("WORKSITE_DEMO_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
        {
            _runner.Say($"Demo account: username '{settings.GetValueOrDefault("WORKSITE_DEMO_USERNAME", "demo")}', password in {_paths.EnvFile} (WORKSITE_DEMO_PASSWORD).");
        }

        _runner.Say("Stop it with `WorkSite.BuildFrontend stop` (the database is kept).");
        return address;
    }

    /// <summary>
    /// <c>stop</c>: stops and removes the two containers; the database volume is kept.
    /// </summary>
    /// <param name="cancellationToken">Stops docker.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Require(await _prerequisites.CheckContainersAsync(cancellationToken).ConfigureAwait(false));
        if (!File.Exists(_paths.EnvFile))
        {
            throw new BuilderException($"{_paths.EnvFile} does not exist, so there is nothing of ours to stop.");
        }

        await _containers.DownAsync(cancellationToken).ConfigureAwait(false);
        _runner.Say("The WorkSite containers are stopped and removed. The database volume was kept.");
    }

    private IReadOnlyDictionary<string, string> LoadSettings(bool requireCertificateFile)
    {
        if (!File.Exists(_paths.EnvFile))
        {
            throw new BuilderException($"{_paths.EnvFile} does not exist. Run `WorkSite.BuildFrontend env` to create it.");
        }

        var settings = EnvFile.Load(_paths.EnvFile);
        var missing = EnvFile.MissingKeys(settings);
        if (missing.Count > 0)
        {
            throw new BuilderException($"{_paths.EnvFile} has no value for: {string.Join(", ", missing)} (see .env.example).");
        }

        if (requireCertificateFile && !File.Exists(settings["WORKSITE_CERT_FILE"]))
        {
            throw new BuilderException($"The certificate file '{settings["WORKSITE_CERT_FILE"]}' does not exist. Run `WorkSite.BuildFrontend cert`.");
        }

        return settings;
    }

    private static void Require(IReadOnlyList<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new BuilderException("Something this step needs is missing:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  - " + p)));
        }
    }
}
