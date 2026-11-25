using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WorkSite.BuildFrontend.Steps;

/// <summary>
/// Publishes the backend, and builds, starts, stops and probes the two WorkSite containers
/// through <c>docker compose</c> and the Compose file in <c>docker/</c>.
/// </summary>
public sealed class ContainerStep
{
    private readonly ToolRunner _runner;
    private readonly BuilderPaths _paths;

    /// <summary>
    /// Creates the step.
    /// </summary>
    /// <param name="runner">Runs dotnet and docker.</param>
    /// <param name="paths">The WorkSite paths.</param>
    public ContainerStep(ToolRunner runner, BuilderPaths paths)
    {
        _runner = runner;
        _paths = paths;
    }

    /// <summary>
    /// The <c>docker compose</c> arguments that select WorkSite's Compose file and settings file.
    /// </summary>
    /// <param name="command">The Compose command and its own arguments.</param>
    public IReadOnlyList<string> ComposeArguments(params string[] command)
    {
        var arguments = new List<string> { "compose", "--file", _paths.ComposeFile, "--env-file", _paths.EnvFile };
        arguments.AddRange(command);
        return arguments;
    }

    /// <summary>
    /// Publishes the backend (Release) into <c>artifacts/publish</c>, replacing what was there.
    /// The built frontend (frontend-built in the backend project folder) is part of the
    /// published output: the platform's Backend package carries it there.
    /// </summary>
    /// <param name="cancellationToken">Stops dotnet.</param>
    /// <exception cref="BuilderException">The publish failed.</exception>
    public async Task PublishBackendAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_paths.PublishDirectory))
        {
            Directory.Delete(_paths.PublishDirectory, recursive: true);
        }

        _runner.Say("Publishing the backend ...");
        var result = await _runner.RunAsync(
            "dotnet",
            new[] { "publish", _paths.ApiProject, "--configuration", "Release", "--output", _paths.PublishDirectory, "--nologo", "--verbosity", "quiet" },
            _paths.Layout.Root,
            echo: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !File.Exists(Path.Combine(_paths.PublishDirectory, "WorkSite.Api.dll")))
        {
            throw new BuilderException("Publishing the backend failed:" + Environment.NewLine + ToolRunner.Tail(result.Output, 40));
        }

        var builtFolder = CodeBrix.WebPlatform.Core.Applications.BuiltFrontend.FolderName;
        if (!File.Exists(Path.Combine(_paths.PublishDirectory, builtFolder, "index.html")))
        {
            throw new BuilderException($"The published backend has no {builtFolder}/index.html; the frontend was not built into it.");
        }
    }

    /// <summary>
    /// Builds the web image and starts both containers in the background.
    /// </summary>
    /// <param name="cancellationToken">Stops docker.</param>
    /// <exception cref="BuilderException">Compose failed.</exception>
    public async Task UpAsync(CancellationToken cancellationToken = default)
    {
        _runner.Say("Building the web image and starting the containers ...");
        var result = await _runner.RunAsync("docker", ComposeArguments("up", "--build", "--detach", "--wait-timeout", "120"), _paths.Layout.Root, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BuilderException("`docker compose up` failed:" + Environment.NewLine + ToolRunner.Tail(result.Output, 40));
        }
    }

    /// <summary>
    /// Stops and removes both containers and their network. The database volume is kept, so
    /// the data is there again on the next start.
    /// </summary>
    /// <param name="cancellationToken">Stops docker.</param>
    /// <exception cref="BuilderException">Compose failed.</exception>
    public async Task DownAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync("docker", ComposeArguments("down"), _paths.Layout.Root, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BuilderException("`docker compose down` failed:" + Environment.NewLine + ToolRunner.Tail(result.Output, 20));
        }
    }

    /// <summary>
    /// Returns the last lines the web container logged.
    /// </summary>
    /// <param name="lines">How many lines.</param>
    /// <param name="cancellationToken">Stops docker.</param>
    public async Task<string> WebLogsAsync(int lines = 40, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync("docker", ComposeArguments("logs", "--no-color", "--tail", lines.ToString(System.Globalization.CultureInfo.InvariantCulture), "web"), _paths.Layout.Root, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Output;
    }

    /// <summary>
    /// Waits until <c>https://localhost:&lt;port&gt;/api/health</c> answers 200. The connection
    /// is accepted only when the server presents exactly the exported development certificate
    /// (compared by SHA-256 thumbprint), so the wait does not depend on what this machine's
    /// certificate stores trust.
    /// </summary>
    /// <param name="port">The HTTPS port.</param>
    /// <param name="certificateThumbprint">The expected certificate's SHA-256 thumbprint (hex).</param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>True when the site answered in time.</returns>
    public static async Task<bool> WaitUntilHealthyAsync(int port, string certificateThumbprint, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate != null && string.Equals(
                        Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())),
                        certificateThumbprint,
                        StringComparison.OrdinalIgnoreCase),
            },
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync($"https://localhost:{port}/api/health", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Not up yet.
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
