using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>One frontend application as <see cref="PlatformFrontend"/> builds it: every path worked out.</summary>
internal sealed class ApplicationPlan
{
    private ApplicationPlan(FrontendApplication application, string frontendDirectory)
    {
        Application = application;
        FrontendDirectory = frontendDirectory;
    }

    /// <summary>The application's name and place.</summary>
    public FrontendApplication Application { get; }

    /// <summary>The frontend folder, as a full path.</summary>
    public string FrontendDirectory { get; }

    /// <summary>Where the generated client code goes.</summary>
    public string GeneratedClientDirectory => Path.Combine(FrontendDirectory, "src", "generated");

    /// <summary>Where the shared crate goes.</summary>
    public string SharedCrateDirectory => Path.Combine(FrontendDirectory, "platform", SharedRustCrate.CrateName);

    /// <summary>The cargo configuration that materializing writes.</summary>
    public string CargoConfigurationFile => Path.Combine(FrontendDirectory, ".cargo", "config.toml");

    /// <summary>The Tailwind input stylesheet.</summary>
    public string TailwindInputFile { get; private set; }

    /// <summary>The generated Tailwind stylesheet.</summary>
    public string TailwindOutputFile { get; private set; }

    /// <summary>dx's own data folder for this application.</summary>
    public string DxHomeDirectory { get; private set; }

    /// <summary>Whether the application is built with bundle splitting.</summary>
    public bool WasmSplit { get; private set; }

    /// <summary>The cargo features for dx serve.</summary>
    public IReadOnlyList<string> ServeFeatures { get; private set; }

    /// <summary>How messages name the application when there are several: " 'sales'", or nothing for a single one.</summary>
    public string Label { get; private set; }

    /// <summary>
    /// Works out the applications of a build program's options.
    /// </summary>
    /// <exception cref="ArgumentException">No frontend folder is named, both forms are used at
    /// once, a name breaks a rule, a name or a folder is used twice, or more than one
    /// application has no name.</exception>
    public static IReadOnlyList<ApplicationPlan> Create(PlatformFrontendOptions options)
    {
        var plans = new List<ApplicationPlan>();
        if (options.Applications.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(options.FrontendDirectory))
            {
                throw new ArgumentException("PlatformFrontendOptions.FrontendDirectory must name the application's frontend folder.", nameof(options));
            }

            var frontend = Path.GetFullPath(options.FrontendDirectory);
            plans.Add(new ApplicationPlan(FromName(options.ApplicationName, nameof(options)), frontend)
            {
                TailwindInputFile = InFrontend(frontend, options.TailwindInputFile, "tailwind.css"),
                TailwindOutputFile = InFrontend(frontend, options.TailwindOutputFile, Path.Combine("assets", "tailwind.css")),
                DxHomeDirectory = string.IsNullOrWhiteSpace(options.DxHomeDirectory) ? Path.Combine(frontend, "target", "dx-home") : Path.GetFullPath(options.DxHomeDirectory),
                WasmSplit = options.WasmSplit ?? false,
                ServeFeatures = options.ServeFeatures.ToArray(),
                Label = string.Empty,
            });
            return plans;
        }

        if (!string.IsNullOrWhiteSpace(options.FrontendDirectory) || !string.IsNullOrEmpty(options.ApplicationName))
        {
            throw new ArgumentException(
                "PlatformFrontendOptions names one frontend (FrontendDirectory, ApplicationName) and a list of them (Applications). Use one form: leave FrontendDirectory and ApplicationName unset when Applications has entries.",
                nameof(options));
        }

        var first = options.Applications[0] ?? throw new ArgumentException("An entry of PlatformFrontendOptions.Applications is null.", nameof(options));
        var firstSplit = first.WasmSplit ?? options.WasmSplit ?? false;
        foreach (var entry in options.Applications)
        {
            if (entry == null)
            {
                throw new ArgumentException("An entry of PlatformFrontendOptions.Applications is null.", nameof(options));
            }

            var application = FromName(entry.Name, nameof(options));
            if (string.IsNullOrWhiteSpace(entry.FrontendDirectory))
            {
                throw new ArgumentException($"The application '{application.Name}' in PlatformFrontendOptions.Applications names no FrontendDirectory.", nameof(options));
            }

            var frontend = Path.GetFullPath(entry.FrontendDirectory);
            var sameFolder = plans.FirstOrDefault(p => string.Equals(p.FrontendDirectory, frontend, StringComparison.Ordinal));
            if (sameFolder != null)
            {
                throw new ArgumentException($"The applications '{sameFolder.Application.Name}' and '{application.Name}' name the same frontend folder, '{frontend}'. Each application has a folder of its own.", nameof(options));
            }

            plans.Add(new ApplicationPlan(application, frontend)
            {
                TailwindInputFile = InFrontend(frontend, entry.TailwindInputFile, "tailwind.css"),
                TailwindOutputFile = InFrontend(frontend, entry.TailwindOutputFile, Path.Combine("assets", "tailwind.css")),
                DxHomeDirectory = string.IsNullOrWhiteSpace(options.DxHomeDirectory)
                    ? Path.Combine(frontend, "target", "dx-home")
                    : Path.Combine(Path.GetFullPath(options.DxHomeDirectory), application.Name),
                WasmSplit = entry.WasmSplit ?? firstSplit,
                ServeFeatures = (entry.ServeFeatures.Count > 0 ? entry.ServeFeatures : options.ServeFeatures).ToArray(),
                Label = options.Applications.Count > 1 ? $" '{application.Name}'" : string.Empty,
            });
        }

        try
        {
            _ = new FrontendApplicationSet(plans.Select(p => p.Application));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(exception.Message.Split(" (Parameter", 2)[0], nameof(options), exception);
        }

        return plans;
    }

    private static FrontendApplication FromName(string name, string parameter)
    {
        try
        {
            return FrontendApplication.FromName(name);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(exception.Message.Split(" (Parameter", 2)[0], parameter, exception);
        }
    }

    private static string InFrontend(string frontend, string path, string fallback)
        => Path.GetFullPath(Path.Combine(frontend, string.IsNullOrWhiteSpace(path) ? fallback : path));
}
