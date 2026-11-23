using System.Collections.Generic;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;

namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>
/// What <see cref="PlatformFrontend.CheckPrerequisitesAsync"/> found: one item per
/// <see cref="PrerequisiteKind"/>, in that order.
/// </summary>
public sealed class PrerequisiteReport
{
    internal PrerequisiteReport(IReadOnlyList<PrerequisiteItem> items, string rustVersion, string tailwindVersion, WasmOptSource wasmOpt, HostTools hostTools, string dxVersion = null, PackagedTailwind tailwind = null)
    {
        Items = items;
        RustVersion = rustVersion;
        TailwindVersion = tailwindVersion;
        WasmOpt = wasmOpt;
        HostTools = hostTools;
        DxVersion = dxVersion;
        Tailwind = tailwind;
    }

    /// <summary>Every finding, in the order of <see cref="PrerequisiteKind"/>.</summary>
    public IReadOnlyList<PrerequisiteItem> Items { get; }

    /// <summary>True when no item is <see cref="PrerequisiteStatus.Missing"/>.</summary>
    public bool IsReady => Items.All(i => i.Status != PrerequisiteStatus.Missing);

    /// <summary>The messages of the missing items; empty when <see cref="IsReady"/>.</summary>
    public IReadOnlyList<string> Problems => Items.Where(i => i.Status == PrerequisiteStatus.Missing).Select(i => i.Message).ToList();

    /// <summary>The version of rustc found (for example "1.100.0"), or <c>null</c>.</summary>
    public string RustVersion { get; }

    /// <summary>The version the packaged Tailwind program reported (for example "4.3.5"), or <c>null</c>
    /// when it was not found or did not start.</summary>
    public string TailwindVersion { get; }

    /// <summary>The packaged Tailwind program that a build would run, or <c>null</c> when it was not found.</summary>
    public PackagedTailwind Tailwind { get; }

    /// <summary>Where wasm-opt comes from.</summary>
    public WasmOptSource WasmOpt { get; }

    /// <summary>The dx and wasm-bindgen programs that a build would run, or <c>null</c> when they were not found.</summary>
    public HostTools HostTools { get; }

    /// <summary>
    /// What dx printed for <c>--version</c> when it was started with the libgit2 library it
    /// needs, for example "dioxus 0.7.10 (57d6794)"; <c>null</c> when it was not found or did
    /// not start.
    /// </summary>
    public string DxVersion { get; }

    /// <summary>The finding for one prerequisite.</summary>
    /// <param name="kind">What was checked.</param>
    /// <returns>The item.</returns>
    public PrerequisiteItem this[PrerequisiteKind kind] => Items.First(i => i.Kind == kind);
}
