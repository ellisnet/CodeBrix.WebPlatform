using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>
/// One frontend application as <see cref="PlatformFrontend"/> builds it: every path worked out.
/// The layout inside a frontend folder is fixed (see <see cref="PlatformFrontendOptions"/>).
/// </summary>
internal sealed class ApplicationPlan
{
    /// <summary>How messages name the application that has no name.</summary>
    internal const string RootApplicationText = "the root application";

    private ApplicationPlan(FrontendApplication application, string frontendDirectory)
    {
        Application = application;
        FrontendDirectory = frontendDirectory;
    }

    /// <summary>The application's name and place.</summary>
    public FrontendApplication Application { get; }

    /// <summary>The frontend folder, as a full path.</summary>
    public string FrontendDirectory { get; }

    /// <summary>Where the generated client code goes: <c>src/generated</c>.</summary>
    public string GeneratedClientDirectory => Path.Combine(FrontendDirectory, "src", "generated");

    /// <summary>Where the shared crate goes: <c>platform/codebrix-webplatform</c>.</summary>
    public string SharedCrateDirectory => Path.Combine(FrontendDirectory, "platform", SharedRustCrate.CrateName);

    /// <summary>The cargo configuration that materializing writes: <c>.cargo/config.toml</c>.</summary>
    public string CargoConfigurationFile => Path.Combine(FrontendDirectory, ".cargo", "config.toml");

    /// <summary>cargo's and dx's output folder: <c>target</c>.</summary>
    public string TargetDirectory => Path.Combine(FrontendDirectory, "target");

    /// <summary>dx's own data folder: <c>target/dx-home</c>.</summary>
    public string DxHomeDirectory => Path.Combine(TargetDirectory, "dx-home");

    /// <summary>The Tailwind input stylesheet: <c>tailwind.css</c> (the developer's own file).</summary>
    public string TailwindInputFile => Path.Combine(FrontendDirectory, "tailwind.css");

    /// <summary>The build-owned folder of generated assets: <c>assets/generated</c>.</summary>
    public string GeneratedAssetsDirectory => Path.Combine(FrontendDirectory, "assets", "generated");

    /// <summary>The generated Tailwind stylesheet: <c>assets/generated/tailwind.css</c>.</summary>
    public string TailwindOutputFile => Path.Combine(GeneratedAssetsDirectory, "tailwind.css");

    /// <summary>Whether the application is built with bundle splitting.</summary>
    public bool WasmSplit { get; private set; }

    /// <summary>
    /// The size from which shared code gets a chunk of its own, in bytes before optimisation
    /// (<see cref="PlatformFrontendOptions.SharedChunkThreshold"/>), resolved.
    /// </summary>
    public int SharedChunkThreshold { get; private set; }

    /// <summary>
    /// The most shared chunks (<see cref="PlatformFrontendOptions.SharedChunkLimit"/>), resolved.
    /// </summary>
    public int SharedChunkLimit { get; private set; }

    /// <summary>The cargo features for dx serve.</summary>
    public IReadOnlyList<string> ServeFeatures { get; private set; }

    /// <summary>True when the options name several applications (messages then say which one).</summary>
    public bool IsOneOfSeveral { get; private set; }

    /// <summary>
    /// How messages name the application: "the frontend" when it is the only one, otherwise
    /// "the root application" or "the frontend application 'sales'".
    /// </summary>
    public string Subject => !IsOneOfSeveral ? "the frontend" : Application.IsDefault ? RootApplicationText : $"the frontend application '{Application.Name}'";

    /// <summary>How a message names an application: "the root application", or "the application 'sales'".</summary>
    /// <param name="application">The application.</param>
    public static string Describe(FrontendApplication application)
        => application.IsDefault ? RootApplicationText : $"the application '{application.Name}'";

    /// <summary>
    /// Works out the applications of a build program's options.
    /// </summary>
    /// <exception cref="ArgumentException">No frontend folder is named, both forms are used at
    /// once, a name breaks a rule, a name or a folder is used twice, or more than one
    /// application has no name, or a shared chunk threshold or limit is below 1.</exception>
    public static IReadOnlyList<ApplicationPlan> Create(PlatformFrontendOptions options)
    {
        var plans = new List<ApplicationPlan>();
        var threshold = RequireAtLeastOne(options.SharedChunkThreshold, "PlatformFrontendOptions.SharedChunkThreshold", nameof(options)) ?? PlatformFrontendOptions.DefaultSharedChunkThreshold;
        var limit = RequireAtLeastOne(options.SharedChunkLimit, "PlatformFrontendOptions.SharedChunkLimit", nameof(options)) ?? PlatformFrontendOptions.DefaultSharedChunkLimit;
        if (options.Applications.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(options.FrontendDirectory))
            {
                throw new ArgumentException("PlatformFrontendOptions.FrontendDirectory must name the application's frontend folder.", nameof(options));
            }

            var frontend = Path.GetFullPath(options.FrontendDirectory);
            plans.Add(new ApplicationPlan(FromName(options.ApplicationName, nameof(options)), frontend)
            {
                WasmSplit = options.WasmSplit ?? false,
                SharedChunkThreshold = threshold,
                SharedChunkLimit = limit,
                ServeFeatures = options.ServeFeatures.ToArray(),
                IsOneOfSeveral = false,
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
                throw new ArgumentException($"{Capitalized(Describe(application))} in PlatformFrontendOptions.Applications names no FrontendDirectory.", nameof(options));
            }

            var frontend = Path.GetFullPath(entry.FrontendDirectory);
            var sameFolder = plans.FirstOrDefault(p => string.Equals(p.FrontendDirectory, frontend, StringComparison.Ordinal));
            if (sameFolder != null)
            {
                throw new ArgumentException($"{Capitalized(Describe(sameFolder.Application))} and {Describe(application)} name the same frontend folder, '{frontend}'. Each application has a folder of its own.", nameof(options));
            }

            plans.Add(new ApplicationPlan(application, frontend)
            {
                WasmSplit = entry.WasmSplit ?? firstSplit,
                SharedChunkThreshold = RequireAtLeastOne(entry.SharedChunkThreshold, $"SharedChunkThreshold of {Describe(application)} in PlatformFrontendOptions.Applications", nameof(options)) ?? threshold,
                SharedChunkLimit = RequireAtLeastOne(entry.SharedChunkLimit, $"SharedChunkLimit of {Describe(application)} in PlatformFrontendOptions.Applications", nameof(options)) ?? limit,
                ServeFeatures = (entry.ServeFeatures.Count > 0 ? entry.ServeFeatures : options.ServeFeatures).ToArray(),
                IsOneOfSeveral = options.Applications.Count > 1,
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

    /// <summary>Refuses a shared chunk threshold or limit below 1.</summary>
    /// <param name="value">The value set, or <c>null</c>.</param>
    /// <param name="what">How the message names the setting.</param>
    /// <param name="parameter">The parameter the exception names.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">The value is below 1.</exception>
    private static int? RequireAtLeastOne(int? value, string what, string parameter)
    {
        if (value is < 1)
        {
            throw new ArgumentException(
                $"{what} is {value.Value}; it must be at least 1 (leave it unset for the default). For one shared chunk with all the shared code, set SharedChunkLimit to 1.",
                parameter);
        }

        return value;
    }

    private static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

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
}
