using System;
using System.IO;

namespace CodeBrix.WebPlatform.FrontendBuilder.MacOSX64;

/// <summary>
/// Names the build tools this package ships for macOS on Intel (x64) build machines (runtime identifier
/// <c>osx-x64</c>) and where a consuming build finds them. The package copies them into the
/// output folder of the project that references it, as <c>frontend-tools/osx-x64/</c>:
/// <c>{application base folder}/frontend-tools/osx-x64/dx</c> and <c>wasm-bindgen</c>, with
/// their licence and third-party notices files beside them.
/// </summary>
/// <remarks>
/// The programs are copied as files. A NuGet package records no file permissions: on Linux
/// and macOS the executable permission is whatever the package extraction and the copy give
/// (observed with the .NET 10 SDK: executable for the owner only), so the code that runs them
/// makes sure they are executable first.
/// </remarks>
public static class HostTools
{
    /// <summary>The runtime identifier of the build machines this package is for.</summary>
    public const string RuntimeIdentifier = "osx-x64";

    /// <summary>The folder, below the application's base folder, that holds one folder per runtime identifier.</summary>
    public const string ToolsFolderName = "frontend-tools";

    /// <summary>The file name of the Dioxus CLI.</summary>
    public const string DxFileName = "dx";

    /// <summary>The file name of the wasm-bindgen CLI.</summary>
    public const string WasmBindgenFileName = "wasm-bindgen";

    /// <summary>The dioxus-cli release <c>dx</c> is built from (with the repository's patches).</summary>
    public const string DxVersion = "0.7.10";

    /// <summary>The wasm-bindgen-cli release <c>wasm-bindgen</c> is built from.</summary>
    public const string WasmBindgenVersion = "0.2.130";

    /// <summary>
    /// The folder that holds this platform's tools below an application's base folder:
    /// <c>{applicationBaseDirectory}/frontend-tools/osx-x64</c>.
    /// </summary>
    /// <param name="applicationBaseDirectory">The application's base folder (normally <see cref="AppContext.BaseDirectory"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="applicationBaseDirectory"/> is null or blank.</exception>
    public static string GetToolDirectory(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        return Path.Combine(applicationBaseDirectory, ToolsFolderName, RuntimeIdentifier);
    }

    /// <summary>
    /// The folder that holds this platform's tools below the running application's base folder
    /// (<see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    public static string GetToolDirectory() => GetToolDirectory(AppContext.BaseDirectory);
}
