using System;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>
/// Finds <c>dx</c> and <c>wasm-bindgen</c>: in the host-tools package's folder beside the build
/// program when it exists, otherwise at the paths named in the options.
/// </summary>
internal static class HostToolsLocator
{
    /// <summary>
    /// Finds the two programs, or says why they cannot be found.
    /// </summary>
    /// <param name="baseDirectory">The build program's folder (normally <see cref="AppContext.BaseDirectory"/>).</param>
    /// <param name="runtimeIdentifier">This machine's runtime identifier.</param>
    /// <param name="dxPath">The options' dx path, or <c>null</c>.</param>
    /// <param name="wasmBindgenPath">The options' wasm-bindgen path, or <c>null</c>.</param>
    /// <param name="tools">The programs, made executable; <c>null</c> when not found.</param>
    /// <returns><c>null</c> when found, otherwise a message that says what to do.</returns>
    public static string TryLocate(string baseDirectory, string runtimeIdentifier, string dxPath, string wasmBindgenPath, out HostTools tools)
    {
        tools = null;
        var suffix = runtimeIdentifier != null && runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
        var packageFolder = Path.Combine(baseDirectory ?? string.Empty, HostTools.FolderName, runtimeIdentifier ?? string.Empty);
        if (runtimeIdentifier != null && Directory.Exists(packageFolder))
        {
            var dx = Path.Combine(packageFolder, HostTools.DxName + suffix);
            var bindgen = Path.Combine(packageFolder, HostTools.WasmBindgenName + suffix);
            if (!File.Exists(dx) || !File.Exists(bindgen))
            {
                return $"The host-tools folder '{packageFolder}' does not hold both {HostTools.DxName}{suffix} and {HostTools.WasmBindgenName}{suffix}. Rebuild the build program so that the {HostTools.GetPackageId(runtimeIdentifier)} package copies them again.";
            }

            return Found(dx, bindgen, HostToolsSource.Package, runtimeIdentifier, out tools);
        }

        if (!string.IsNullOrWhiteSpace(dxPath) || !string.IsNullOrWhiteSpace(wasmBindgenPath))
        {
            if (string.IsNullOrWhiteSpace(dxPath) || string.IsNullOrWhiteSpace(wasmBindgenPath))
            {
                return "Both DxPath and WasmBindgenPath must be set in the options when either is; only one is.";
            }

            var dx = Path.GetFullPath(dxPath);
            var bindgen = Path.GetFullPath(wasmBindgenPath);
            if (!File.Exists(dx))
            {
                return $"dx was not found at '{dx}' (the options' DxPath).";
            }

            if (!File.Exists(bindgen))
            {
                return $"wasm-bindgen was not found at '{bindgen}' (the options' WasmBindgenPath).";
            }

            return Found(dx, bindgen, HostToolsSource.Options, runtimeIdentifier, out tools);
        }

        var package = HostTools.GetPackageId(runtimeIdentifier);
        return package == null
            ? $"dx and wasm-bindgen are not available for this machine ('{runtimeIdentifier}'). A platform application can be built on {string.Join(", ", HostTools.SupportedRuntimeIdentifiers)} only."
            : $"dx and wasm-bindgen were not found. Add the {package} package to the build program (it copies them beside it, into {HostTools.FolderName}/{runtimeIdentifier}), or name both programs in the options (DxPath, WasmBindgenPath).";
    }

    private static string Found(string dx, string bindgen, HostToolsSource source, string runtimeIdentifier, out HostTools tools)
    {
        Executables.EnsureExecutable(dx);
        Executables.EnsureExecutable(bindgen);
        tools = new HostTools(dx, bindgen, source, runtimeIdentifier);
        return null;
    }
}
