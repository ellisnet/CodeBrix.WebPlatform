using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeBrix.RustTools.Toolchain;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;

namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>
/// Turns what was found on the machine into <see cref="PrerequisiteItem"/>s. Kept apart from the
/// probing so that the judgements can be tested without the programs.
/// </summary>
internal static class PrerequisiteRules
{
    /// <summary>What the platform asks of the Rust toolchain. A C compiler is not needed.</summary>
    public static ToolchainRequirements CreateRustRequirements()
    {
        var requirements = new ToolchainRequirements
        {
            MinimumRustVersion = ToolVersion.Parse(FrontendRequirements.MinimumRustVersion),
            RequireCCompiler = false,
        };
        requirements.RequiredTargets.Add(FrontendRequirements.WebAssemblyTarget);
        return requirements;
    }

    /// <summary>Judges a detected Rust toolchain.</summary>
    /// <param name="toolchain">What RustTools' detector found.</param>
    public static PrerequisiteItem EvaluateRust(RustToolchainInfo toolchain)
    {
        var readiness = ToolchainReadiness.Evaluate(toolchain, CreateRustRequirements());
        var version = VersionText(toolchain.Rustc?.Version);
        if (readiness.IsReady)
        {
            return new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Ok, $"Rust {version} with the {FrontendRequirements.WebAssemblyTarget} target.");
        }

        var text = new StringBuilder($"Rust {FrontendRequirements.MinimumRustVersion} or later with the {FrontendRequirements.WebAssemblyTarget} target is needed");
        text.Append(version == null ? "." : $" (found Rust {version}).");
        foreach (var issue in readiness.Issues.Where(i => i.IsBlocking))
        {
            text.Append(' ').Append(issue.Message.TrimEnd('.')).Append('.');
        }

        foreach (var guidance in InstallGuidanceProvider.ForReadiness(readiness))
        {
            text.Append(Environment.NewLine).Append("    ").Append(guidance.Title);
            foreach (var command in guidance.Commands)
            {
                text.Append(Environment.NewLine).Append("        ").Append(command);
            }
        }

        return new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Missing, text.ToString());
    }

    /// <summary>Judges the Tailwind program's version against the minimum.</summary>
    /// <param name="found">The version the program reported, or <c>null</c> when it was not found.</param>
    /// <param name="minimum">The oldest accepted version, as "major.minor.patch".</param>
    public static PrerequisiteItem EvaluateTailwind(string found, string minimum)
    {
        if (!Version.TryParse(minimum ?? string.Empty, out var least))
        {
            return new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Missing,
                $"MinimumTailwindVersion '{minimum}' is not a version; give it as major.minor.patch, for example {FrontendRequirements.MinimumTailwindVersion}.");
        }

        if (found == null)
        {
            return new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Missing,
                $"The standalone Tailwind program was not found on PATH. Install `{FrontendRequirements.TailwindProgram}`, version {minimum} or later, and put it on PATH.");
        }

        if (!Version.TryParse(found, out var actual) || actual < least)
        {
            return new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Missing,
                $"The Tailwind program on PATH is version {found}; version {minimum} or later is needed.");
        }

        return new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Ok, $"Tailwind {found} ({minimum} or later is needed).");
    }

    /// <summary>Describes where wasm-opt comes from.</summary>
    /// <param name="source">What was found.</param>
    /// <param name="runtimeIdentifier">This machine's runtime identifier.</param>
    public static PrerequisiteItem DescribeWasmOpt(WasmOptSource source, string runtimeIdentifier)
        => source switch
        {
            WasmOptSource.Package => new PrerequisiteItem(PrerequisiteKind.WasmOpt, PrerequisiteStatus.Ok,
                "wasm-opt comes from the CodeBrix.WebPlatform.WasmShrink package."),
            WasmOptSource.Installed => new PrerequisiteItem(PrerequisiteKind.WasmOpt, PrerequisiteStatus.Note,
                $"The CodeBrix.WebPlatform.WasmShrink package does not include wasm-opt for '{runtimeIdentifier}' yet; the one installed on PATH is used."),
            _ => new PrerequisiteItem(PrerequisiteKind.WasmOpt, PrerequisiteStatus.Missing,
                $"wasm-opt is not available: the CodeBrix.WebPlatform.WasmShrink package does not include it for '{runtimeIdentifier}' yet, and none is installed on PATH. A release build of the frontend needs it."),
        };

    /// <summary>Describes the dx and wasm-bindgen programs, or why they were not found.</summary>
    /// <param name="tools">The programs, or <c>null</c>.</param>
    /// <param name="problem">Why they were not found, when <paramref name="tools"/> is <c>null</c>.</param>
    public static PrerequisiteItem DescribeHostTools(HostTools tools, string problem)
    {
        if (tools == null)
        {
            return new PrerequisiteItem(PrerequisiteKind.HostTools, PrerequisiteStatus.Missing, problem);
        }

        return tools.Source == HostToolsSource.Package
            ? new PrerequisiteItem(PrerequisiteKind.HostTools, PrerequisiteStatus.Ok, $"dx and wasm-bindgen come from the {HostTools.GetPackageId(tools.RuntimeIdentifier)} package.")
            : new PrerequisiteItem(PrerequisiteKind.HostTools, PrerequisiteStatus.Ok, $"dx and wasm-bindgen are the programs named in the options ({tools.DxPath}, {tools.WasmBindgenPath}).");
    }

    /// <summary>
    /// Describes the libgit2 library dx needs: prepared and dx started with it, or why not.
    /// </summary>
    /// <param name="supply">The prepared library, or <c>null</c> when it could not be prepared.</param>
    /// <param name="problem">Why it could not be prepared, when <paramref name="supply"/> is <c>null</c>.</param>
    /// <param name="startFailure">What dx wrote when it did not start with the library, or <c>null</c> when it started.</param>
    public static PrerequisiteItem DescribeLibgit2(Libgit2Supply supply, string problem, string startFailure)
    {
        if (supply == null)
        {
            return problem != null && problem.StartsWith("not checked", StringComparison.Ordinal)
                ? new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Note, "libgit2 " + problem)
                : new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Missing, problem);
        }

        if (!supply.IsNeeded)
        {
            return new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Note, "This dx does not link libgit2 (an older build); nothing is supplied to it.");
        }

        if (startFailure != null)
        {
            return new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Missing,
                $"dx did not start with libgit2 '{supply.LoaderFileName}' from {supply.Directory} on {supply.Naming.LoaderVariable}:" + Environment.NewLine + startFailure);
        }

        return new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Ok,
            $"libgit2 ({supply.NeededName}, which dx needs to start) comes from the {Libgit2Supply.PackageId} package {supply.PackageVersion}; dx is given it in {supply.Directory} on {supply.Naming.LoaderVariable}.");
    }

    /// <summary>"1.100.0" for a version, <c>null</c> for none.</summary>
    /// <param name="version">A tool version, or <c>null</c>.</param>
    public static string VersionText(ToolVersion version)
        => version == null ? null : $"{version.Major}.{version.Minor}.{version.Patch}";

    /// <summary>Puts the findings in the order of <see cref="PrerequisiteKind"/>.</summary>
    /// <param name="items">The findings.</param>
    public static IReadOnlyList<PrerequisiteItem> Ordered(IEnumerable<PrerequisiteItem> items)
        => items.OrderBy(i => (int)i.Kind).ToList();

    /// <summary>
    /// The findings a build or <c>dx serve</c> cannot start without: every missing one, except a
    /// missing wasm-opt when nothing will be shrunk.
    /// </summary>
    /// <param name="items">The findings of the check.</param>
    /// <param name="needsWasmOpt">Whether wasm-opt will be run.</param>
    public static IReadOnlyList<PrerequisiteItem> Blocking(IEnumerable<PrerequisiteItem> items, bool needsWasmOpt)
        => items.Where(i => i.Status == PrerequisiteStatus.Missing && (needsWasmOpt || i.Kind != PrerequisiteKind.WasmOpt)).ToList();

    /// <summary>
    /// The message of a build or <c>dx serve</c> refused before it started: what is missing and,
    /// for each, how to fix it (the check's own messages).
    /// </summary>
    /// <param name="what">What was refused, as the sentence's subject: "The build", "dx serve".</param>
    /// <param name="blocking">The missing prerequisites (<see cref="Blocking"/>).</param>
    public static string Refusal(string what, IReadOnlyList<PrerequisiteItem> blocking)
    {
        var text = new StringBuilder($"{what} cannot start: this machine is missing what it needs.");
        foreach (var item in blocking)
        {
            text.Append(Environment.NewLine).Append("  - ").Append(item.Message.Replace(Environment.NewLine, Environment.NewLine + "    "));
        }

        text.Append(Environment.NewLine).Append("Install what is missing, then try again (PlatformFrontend.CheckPrerequisitesAsync reports every prerequisite).");
        return text.ToString();
    }
}
