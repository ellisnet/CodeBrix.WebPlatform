using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WorkSite.BuildFrontend.Steps;

/// <summary>
/// WorkSite's own prerequisites, beside the ones CodeBrix.WebPlatform.FrontendBuilder checks
/// for every platform frontend: Docker for the containers. Nothing is ever installed by the
/// build program (Tailwind comes in the host-tools package, through its Styling package).
/// </summary>
public sealed class Prerequisites
{
    private readonly ToolRunner _runner;
    private readonly BuilderPaths _paths;

    /// <summary>
    /// Creates the checker.
    /// </summary>
    /// <param name="runner">Runs the probes.</param>
    /// <param name="paths">The WorkSite paths.</param>
    public Prerequisites(ToolRunner runner, BuilderPaths paths)
    {
        _runner = runner;
        _paths = paths;
    }

    /// <summary>
    /// Checks what running the containers needs: Docker with Compose, and a running engine.
    /// </summary>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The problems found; empty when everything is in place.</returns>
    public async Task<IReadOnlyList<string>> CheckContainersAsync(CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        var compose = await _runner.RunAsync("docker", new[] { "compose", "version" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (compose.NotFound)
        {
            problems.Add("docker was not found. Install Docker (Docker Engine on Linux, Docker Desktop on Windows and macOS).");
        }
        else if (!compose.Succeeded)
        {
            problems.Add("`docker compose` is not available. Install the Docker Compose plugin.");
        }
        else
        {
            var info = await _runner.RunAsync("docker", new[] { "info", "--format", "{{.ServerVersion}}" }, echo: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!info.Succeeded)
            {
                problems.Add("The Docker engine is not running, or this user may not use it.");
            }
        }

        return problems;
    }
}
