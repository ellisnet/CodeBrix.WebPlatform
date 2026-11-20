using System;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Who shrinks the WebAssembly file of one build, decided from the configuration and
/// <see cref="PlatformFrontendOptions.Shrink"/>:
/// <list type="table">
/// <item><term>Release, Shrink unset or true</term><description>dx runs wasm-opt (it does on every release build).</description></item>
/// <item><term>Release, Shrink false</term><description>dx is asked to skip wasm-opt (<c>DX_SKIP_WASM_OPT</c>); no wasm-opt is needed.</description></item>
/// <item><term>Debug, Shrink true</term><description>the library runs wasm-opt on dx's output.</description></item>
/// <item><term>Debug, Shrink unset or false</term><description>nobody shrinks it.</description></item>
/// </list>
/// </summary>
internal readonly struct ShrinkPlan
{
    /// <summary>The environment variable that asks the platform's dx to skip wasm-opt on a release build.</summary>
    public const string SkipVariable = "DX_SKIP_WASM_OPT";

    /// <summary>The start of the line the platform's dx prints when it skipped wasm-opt on request.</summary>
    public const string SkipConfirmationText = "Skipping wasm-opt at the caller's request";

    private ShrinkPlan(bool shrink, bool dxShrinks, bool libraryShrinks, bool skipRequested)
    {
        Shrink = shrink;
        DxShrinks = dxShrinks;
        LibraryShrinks = libraryShrinks;
        SkipRequested = skipRequested;
    }

    /// <summary>True when the file is to be shrunk.</summary>
    public bool Shrink { get; }

    /// <summary>True when dx runs wasm-opt (a release build that is shrunk).</summary>
    public bool DxShrinks { get; }

    /// <summary>True when the library runs wasm-opt after dx (a debug build that is shrunk).</summary>
    public bool LibraryShrinks { get; }

    /// <summary>True when dx is asked to skip wasm-opt (a release build that is not shrunk).</summary>
    public bool SkipRequested { get; }

    /// <summary>True when a wasm-opt must be available before the build starts.</summary>
    public bool NeedsWasmOpt => DxShrinks || LibraryShrinks;

    /// <summary>
    /// The value dx is given for <see cref="SkipVariable"/>: "1" to skip, "0" otherwise, so a
    /// value left in the developer's environment never decides it.
    /// </summary>
    public string SkipVariableValue => SkipRequested ? "1" : "0";

    /// <summary>Decides who shrinks the file.</summary>
    /// <param name="configuration">The kind of build.</param>
    /// <param name="shrinkOption"><see cref="PlatformFrontendOptions.Shrink"/>.</param>
    /// <param name="wasmSplit">Whether the build splits the bundle: dx then always runs wasm-opt
    /// (on the main file and on every piece), whatever the configuration and the option.</param>
    public static ShrinkPlan Decide(FrontendConfiguration configuration, bool? shrinkOption, bool wasmSplit = false)
    {
        if (wasmSplit)
        {
            return new ShrinkPlan(true, true, false, false);
        }

        var release = configuration == FrontendConfiguration.Release;
        var shrink = shrinkOption ?? release;
        return new ShrinkPlan(shrink, release && shrink, !release && shrink, release && !shrink);
    }

    /// <summary>
    /// True when dx itself ran wasm-opt in this build. A split build always does. A release build always does, unless the
    /// skip was asked for AND dx confirms it in its output (a dx without the platform's patch
    /// ignores the request and shrinks anyway).
    /// </summary>
    /// <param name="configuration">The kind of build.</param>
    /// <param name="dxOutput">Everything dx wrote.</param>
    public bool DxRanWasmOpt(FrontendConfiguration configuration, string dxOutput)
        => (DxShrinks || configuration == FrontendConfiguration.Release)
            && !(SkipRequested && (dxOutput ?? string.Empty).Contains(SkipConfirmationText, StringComparison.Ordinal));
}
