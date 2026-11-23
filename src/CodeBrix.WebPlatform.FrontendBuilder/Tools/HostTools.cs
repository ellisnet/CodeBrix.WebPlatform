using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.WebPlatform.WasmShrink;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>
/// The two programs a frontend build runs on the developer's machine: the Dioxus build tool
/// <c>dx</c> and the <c>wasm-bindgen</c> program it calls. They come from a host-tools package,
/// one per build platform, that the build program references; or, without one, from paths the
/// build program names in its options.
/// </summary>
public sealed class HostTools
{
    /// <summary>
    /// The folder, beside the build program, that a host-tools package copies its programs into:
    /// <c>&lt;build program folder&gt;/frontend-tools/&lt;runtime identifier&gt;/dx[.exe]</c> and
    /// <c>.../wasm-bindgen[.exe]</c>.
    /// </summary>
    public const string FolderName = "frontend-tools";

    /// <summary>The file name of the Dioxus build tool, without the Windows suffix.</summary>
    public const string DxName = "dx";

    /// <summary>The file name of the wasm-bindgen program, without the Windows suffix.</summary>
    public const string WasmBindgenName = "wasm-bindgen";

    private static readonly IReadOnlyDictionary<string, string> PackagePlatforms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["linux-x64"] = "LinuxX64",
        ["linux-arm64"] = "LinuxArm64",
        ["win-x64"] = "WindowsX64",
        ["osx-arm64"] = "MacOSArm64",
        ["osx-x64"] = "MacOSX64",
    };

    internal HostTools(string dxPath, string wasmBindgenPath, HostToolsSource source, string runtimeIdentifier)
    {
        DxPath = dxPath;
        WasmBindgenPath = wasmBindgenPath;
        Source = source;
        RuntimeIdentifier = runtimeIdentifier;
    }

    /// <summary>
    /// The runtime identifiers of the five machines a platform application can be built on:
    /// linux-x64, linux-arm64, win-x64, osx-arm64 and osx-x64.
    /// </summary>
    public static IReadOnlyList<string> SupportedRuntimeIdentifiers { get; } = new[] { "linux-x64", "linux-arm64", "win-x64", "osx-arm64", "osx-x64" };

    /// <summary>The runtime identifier of this machine, for example "linux-x64".</summary>
    public static string CurrentRuntimeIdentifier => WasmShrinkTool.CurrentRuntimeIdentifier;

    /// <summary>The full path of <c>dx</c>.</summary>
    public string DxPath { get; }

    /// <summary>The full path of <c>wasm-bindgen</c>.</summary>
    public string WasmBindgenPath { get; }

    /// <summary>The folder that holds <c>wasm-bindgen</c>; it is put on the PATH dx runs with.</summary>
    public string WasmBindgenDirectory => Path.GetDirectoryName(WasmBindgenPath);

    /// <summary>Where the two programs come from.</summary>
    public HostToolsSource Source { get; }

    /// <summary>The runtime identifier of the machine they were found for.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>
    /// The id of the host-tools package for a machine, for example
    /// <c>CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever</c> for "linux-x64".
    /// </summary>
    /// <param name="runtimeIdentifier">A runtime identifier.</param>
    /// <returns>The package id, or <c>null</c> for a machine the platform does not build on.</returns>
    public static string GetPackageId(string runtimeIdentifier)
    {
        var platform = GetPlatformName(runtimeIdentifier);
        return platform == null ? null : $"CodeBrix.WebPlatform.FrontendBuilder.{platform}.ApacheLicenseForever";
    }

    /// <summary>
    /// The platform part of the per-platform package ids for a machine, for example
    /// <c>LinuxX64</c> for "linux-x64".
    /// </summary>
    /// <param name="runtimeIdentifier">A runtime identifier.</param>
    /// <returns>The platform name, or <c>null</c> for a machine the platform does not build on.</returns>
    internal static string GetPlatformName(string runtimeIdentifier)
        => runtimeIdentifier != null && PackagePlatforms.TryGetValue(runtimeIdentifier, out var platform) ? platform : null;
}
