using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;

namespace ApplicationSplitter.BuildFrontend;

/// <summary>
/// The build program of the ApplicationSplitter sample. It tells the platform's FrontendBuilder
/// about the sample's THREE frontend applications - they share the one backend - and lets it
/// check the machine, materialize them, build them into the backend's web root, or serve one of
/// them with hot reload.
/// </summary>
public static class Program
{
    private const string Usage = """
        Usage: dotnet run --project build/ApplicationSplitter.BuildFrontend -- <command>

          check            say what this machine has and what it lacks for a build
          generate         write the generated client code, the shared crate and the crates
                           into each of the three frontends (after this, plain cargo works there)
          build [--debug]  generate, then build the three frontends into the backend's wwwroot
          run [--debug]    build, then start the backend (http://localhost:5210/)
          serve [name]     generate, then run `dx serve` (hot reload) for one application:
                           no name for the one at the site root, or sales, or admin
        """;

    /// <summary>Runs one command.</summary>
    /// <param name="args">The command and its options.</param>
    /// <returns>0 when it worked.</returns>
    public static async Task<int> Main(string[] args)
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        var root = FindSampleRoot();
        var frontend = new PlatformFrontend(CreateOptions(root, Console.Out));
        var configuration = args.Contains("--debug") ? FrontendConfiguration.Debug : FrontendConfiguration.Release;
        try
        {
            switch (args.FirstOrDefault())
            {
                case "check":
                    var report = await frontend.CheckPrerequisitesAsync(stop.Token);
                    foreach (var item in report.Items)
                    {
                        Console.WriteLine($"  {item.Kind,-12} {item.Status,-8} {item.Message}");
                    }

                    return report.Items.All(i => i.Status != CodeBrix.WebPlatform.FrontendBuilder.Prerequisites.PrerequisiteStatus.Missing) ? 0 : 1;

                case "generate":
                    await frontend.MaterializeAsync(stop.Token);
                    return 0;

                case "build":
                    await BuildAsync(frontend, root, configuration, stop.Token);
                    return 0;

                case "run":
                    await BuildAsync(frontend, root, configuration, stop.Token);
                    return Run("dotnet", $"run --project \"{BackendProject(root)}\" --configuration Release --no-launch-profile --urls http://localhost:5210", root);

                case "serve":
                    await frontend.MaterializeAsync(stop.Token);
                    return await frontend.ServeAsync(args.Skip(1).FirstOrDefault(a => !a.StartsWith('-')), stop.Token);

                default:
                    Console.WriteLine(Usage);
                    return args.Length == 0 ? 0 : 2;
            }
        }
        catch (FrontendBuildException exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// The sample's frontend applications. The first has no name, so it is served at the site
    /// root; "sales" and "admin" are served below their names. "sales" is built with bundle
    /// splitting and "admin" is not.
    /// </summary>
    /// <param name="root">The sample's folder.</param>
    /// <param name="output">Where progress is written.</param>
    /// <returns>The options for the platform's FrontendBuilder.</returns>
    public static PlatformFrontendOptions CreateOptions(string root, TextWriter output)
    {
        var options = new PlatformFrontendOptions
        {
            BackendProjectPath = BackendProject(root),
            CrateMode = CrateMode.PlatformCratesOnly,
            Output = output,
        };
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Path.Combine(root, "frontends", "home") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Path.Combine(root, "frontends", "sales"), WasmSplit = true });
        options.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = Path.Combine(root, "frontends", "admin"), WasmSplit = false });
        return options;
    }

    private static async Task BuildAsync(PlatformFrontend frontend, string root, FrontendConfiguration configuration, CancellationToken cancellationToken)
    {
        await frontend.MaterializeAsync(cancellationToken);
        var webRoot = Path.Combine(root, "backend", "ApplicationSplitter.Api", "wwwroot");
        var built = await frontend.BuildAsync(configuration, webRoot, cancellationToken);
        File.WriteAllText(Path.Combine(webRoot, "THIRD-PARTY-NOTICES.txt"), PlatformCrates.ReadNotices());
        foreach (var application in built.Applications)
        {
            Console.WriteLine($"  {application.ApplicationName,-6} {application.WebRootFileCount,3} files in {application.WebRootApplicationDirectory}"
                + (application.WasmSplit ? " (bundle splitting)" : string.Empty));
        }
    }

    private static string BackendProject(string root)
        => Path.Combine(root, "backend", "ApplicationSplitter.Api", "ApplicationSplitter.Api.csproj");

    private static string FindSampleRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApplicationSplitter.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The sample's folder (the one with ApplicationSplitter.slnx) was not found above " + AppContext.BaseDirectory);
    }

    private static int Run(string program, string arguments, string workingDirectory)
    {
        using var process = Process.Start(new ProcessStartInfo(program, arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false });
        process.WaitForExit();
        return process.ExitCode;
    }
}
