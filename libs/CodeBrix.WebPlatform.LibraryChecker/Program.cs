using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.LibraryChecker.Maintenance;

namespace CodeBrix.WebPlatform.LibraryChecker;

/// <summary>
/// The LibraryChecker command line: the maintainer tool of the CodeBrix.WebPlatform repository.
/// </summary>
public static class Program
{
    private const string Usage = """
        LibraryChecker - the maintainer gate of the CodeBrix.WebPlatform repository. It checks what the
        repository holds outside samples/; it is not published and is no part of any package.

        Usage:  LibraryChecker [<check>] [--write] [--root <repository root>]
                LibraryChecker revendor | revendor-tools
                LibraryChecker proof [--frontend <materialized frontend folder>]

        Checks:
          all          every check below (the default)
          no-node      no package.json, node_modules or JavaScript of ours outside samples/
          vendor       the platform's crates and the tools' crates are untouched and match their
                       Cargo.lock files; the shared crate needs only the platform's crates; no
                       libgit2 source anywhere (the libgit2-sys stand-in compiles no C code); git
                       hides no vendored or tool-source file
          patches      each tool source is its crates.io archive plus the recorded patches
          licenses     every crate's license is on the allowlist for its tier
          notices      the generated notices are up to date (--write regenerates them)

        Maintenance (not checks):
          revendor       re-take the platform's crates from crates.io (needs the network)
          revendor-tools re-take the build tools' crates from crates.io (needs the network)
          proof          build a materialized frontend (default: the sample's) in a container with
                         no Node.js and no network, with only the packaged tools (needs Docker)

        Exit code 0 when every check passes, 1 when one fails, 2 for a usage error.
        """;

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 when every check passed, 1 when one failed, 2 for a usage error.</returns>
    public static async Task<int> Main(string[] args)
    {
        var group = "all";
        var write = false;
        string root = null;
        string frontend = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    Console.WriteLine(Usage);
                    return 0;
                case "--write":
                    write = true;
                    break;
                case "--root" when i + 1 < args.Length:
                    root = args[++i];
                    break;
                case "--frontend" when i + 1 < args.Length:
                    frontend = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-') || group != "all")
                    {
                        Console.Error.WriteLine($"Unexpected argument '{args[i]}'.{Environment.NewLine}{Environment.NewLine}{Usage}");
                        return 2;
                    }

                    group = args[i];
                    break;
            }
        }

        var layout = root != null
            ? (RepositoryLayout.IsRoot(root) ? new RepositoryLayout(root) : null)
            : RepositoryLayout.Find(Directory.GetCurrentDirectory()) ?? RepositoryLayout.Find(AppContext.BaseDirectory);
        if (layout == null)
        {
            Console.Error.WriteLine($"The CodeBrix.WebPlatform repository root (the folder with {RepositoryLayout.SolutionFile} and {RepositoryLayout.MarkerFile}) was not found. Run from inside it or pass --root.");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            switch (group.ToLowerInvariant())
            {
                case "revendor":
                    await new VendorCommand(layout, Console.Out).VendorPlatformAsync(cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "revendor-tools":
                    await new VendorCommand(layout, Console.Out).VendorToolsAsync(cancellation.Token).ConfigureAwait(false);
                    return 0;
                case "proof":
                    var proof = new NodeFreeProof(frontend ?? NodeFreeProof.DefaultFrontend(layout), Console.Out);
                    var files = await proof.RunAsync(cancellation.Token).ConfigureAwait(false);
                    Console.WriteLine("The frontend built in a container with no Node.js and no network, with only the packaged tools. It produced:");
                    foreach (var file in files)
                    {
                        Console.WriteLine("    " + file);
                    }

                    return 0;
            }

            var results = await CheckRunner.RunAsync(layout, group, write, cancellation.Token).ConfigureAwait(false);
            foreach (var result in results)
            {
                Console.WriteLine(result.ToText());
            }

            var failed = results.Count(r => !r.Passed);
            Console.WriteLine();
            Console.WriteLine(failed == 0 ? $"All {results.Count} checks passed." : $"{failed} of {results.Count} checks FAILED.");
            return failed == 0 ? 0 : 1;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (MaintenanceException exception)
        {
            Console.Error.WriteLine("FAILED: " + exception.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or CodeBrix.RustTools.RustToolsException)
        {
            Console.Error.WriteLine("LibraryChecker could not run: " + exception.Message);
            return 1;
        }
    }
}
