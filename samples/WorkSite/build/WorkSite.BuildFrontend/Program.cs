using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder;

namespace WorkSite.BuildFrontend;

/// <summary>
/// The command line of WorkSite.BuildFrontend, WorkSite's build program.
/// </summary>
public static class Program
{
    private const string Usage = """
        WorkSite.BuildFrontend - builds and runs the WorkSite sample. Works the same on Windows, macOS and Linux.

        Usage:  dotnet run --project build/WorkSite.BuildFrontend -- <command> [--root <WorkSite folder>]

        Everyday:
          run            check, build everything, start the two containers, print the site's address
                         (--skip-build reuses the frontend already built into the backend)
          stop           stop and remove the containers (the database is kept)
          build          generate, then build the frontend, with the notices, into the backend
                         project's frontend-built folder
          generate       build the backend, generate the Rust client code for its endpoints and hubs,
                         and write the platform's shared crate, crates and cargo configuration into
                         the frontend (after this, plain cargo works there, offline)
          serve          dx serve with hot reload, for frontend development
          check          report whether everything needed is installed, and where dx, wasm-bindgen
                         and wasm-opt come from (installs nothing)

        Set-up:
          env            create .env with random secrets, if it does not exist
          cert           export the HTTPS development certificate for the web container

        Exit code 0 on success, 1 when a step failed, 2 for a usage error.
        """;

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 on success, 1 when a step failed, 2 for a usage error.</returns>
    public static async Task<int> Main(string[] args)
    {
        string command = null;
        string root = null;
        var skipBuild = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    Console.WriteLine(Usage);
                    return 0;
                case "--skip-build":
                    skipBuild = true;
                    break;
                case "--root" when i + 1 < args.Length:
                    root = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-') || command != null)
                    {
                        Console.Error.WriteLine($"Unexpected argument '{args[i]}'.{Environment.NewLine}{Environment.NewLine}{Usage}");
                        return 2;
                    }

                    command = args[i];
                    break;
            }
        }

        if (command == null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var layout = root != null
            ? (WorkSiteLayout.IsRoot(root) ? new WorkSiteLayout(root) : null)
            : WorkSiteLayout.Find(Directory.GetCurrentDirectory()) ?? WorkSiteLayout.Find(AppContext.BaseDirectory);
        if (layout == null)
        {
            Console.Error.WriteLine("The WorkSite folder (samples/WorkSite) was not found. Run from inside it or pass --root.");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        var builder = new Builder(layout, Console.Out);
        try
        {
            switch (command.ToLowerInvariant())
            {
                case "check":
                    return await builder.CheckAsync(cancellation.Token).ConfigureAwait(false) ? 0 : 1;
                case "generate":
                    await builder.GenerateAsync(cancellationToken: cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "build":
                    await builder.BuildAsync(cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "serve":
                    return await builder.ServeAsync(cancellation.Token).ConfigureAwait(false);
                case "env":
                    builder.CreateEnvFile();
                    return 0;
                case "cert":
                    await builder.ExportCertificateAsync(cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "run":
                    await builder.RunAsync(skipBuild, cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "stop":
                    await builder.StopAsync(cancellation.Token).ConfigureAwait(false);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.{Environment.NewLine}{Environment.NewLine}{Usage}");
                    return 2;
            }
        }
        catch (Exception exception) when (exception is BuilderException or FrontendBuildException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("FAILED: " + exception.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 1;
        }
    }
}
