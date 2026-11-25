using System.Collections.Generic;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// What <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/>
/// (or the one-application form) built.
/// </summary>
public sealed class FrontendBuildResult
{
    internal FrontendBuildResult(FrontendConfiguration configuration, string outputDirectory, bool shrunk, WasmOptSource wasmOpt, HostTools tools, string builtFrontendDirectory, int builtFileCount)
    {
        Configuration = configuration;
        OutputDirectory = outputDirectory;
        Shrunk = shrunk;
        WasmOpt = wasmOpt;
        Tools = tools;
        BuiltFrontendDirectory = builtFrontendDirectory;
        BuiltFileCount = builtFileCount;
        Applications = new[] { this };
    }

    /// <summary>
    /// The name of the application this result is about: "app" for the root application (the
    /// one that has no name of its own, at the site root).
    /// </summary>
    public string ApplicationName { get; internal set; } = CodeBrix.WebPlatform.Core.Applications.FrontendApplication.DefaultName;

    /// <summary>
    /// One result per frontend application that was built, in the order of the options; the
    /// first one is this object. A build program with one application, and the one-application
    /// form of the build, read this object and can ignore the list.
    /// </summary>
    public IReadOnlyList<FrontendBuildResult> Applications { get; internal set; }

    /// <summary>Whether this application was built with bundle splitting.</summary>
    public bool WasmSplit { get; internal set; }

    /// <summary>
    /// The folder, inside <see cref="BuiltFrontendDirectory"/>, that holds this application's
    /// site: that folder itself for the root application, otherwise the sub-folder of the
    /// application's name. <c>null</c> when no backend project folder was given.
    /// </summary>
    public string BuiltApplicationDirectory { get; internal set; }

    /// <summary>
    /// The frontend manifest that was written into <see cref="BuiltFrontendDirectory"/> for the
    /// backend, or <c>null</c> when no backend project folder was given.
    /// </summary>
    public string ManifestFile { get; internal set; }

    /// <summary>
    /// The compressed copies written beside this application's files in
    /// <see cref="BuiltFrontendDirectory"/> (see <see cref="PlatformFrontendOptions.CompressOutput"/>):
    /// which files, their sizes before and after, and the time it took. <c>null</c> when no
    /// backend project folder was given.
    /// </summary>
    public FrontendCompressionResult Compression { get; internal set; }

    /// <summary>The kind of build that was made.</summary>
    public FrontendConfiguration Configuration { get; }

    /// <summary>The folder dx left the servable site in (it holds <c>index.html</c>).</summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// True when the WebAssembly file was shrunk by wasm-opt: by dx in a release build (unless
    /// <see cref="PlatformFrontendOptions.Shrink"/> was <c>false</c> and dx confirmed that it
    /// skipped wasm-opt), or by this library in a debug build with Shrink <c>true</c>.
    /// </summary>
    public bool Shrunk { get; }

    /// <summary>
    /// Where the wasm-opt that shrank the file came from; <see cref="WasmOptSource.Missing"/>
    /// when the build was not shrunk.
    /// </summary>
    public WasmOptSource WasmOpt { get; }

    /// <summary>The dx and wasm-bindgen programs the build ran.</summary>
    public HostTools Tools { get; }

    /// <summary>
    /// The built frontend folder the site was copied into - <see cref="Core.Applications.BuiltFrontend.FolderName"/>
    /// in the backend project folder given to the build - or <c>null</c> when none was given.
    /// </summary>
    public string BuiltFrontendDirectory { get; }

    /// <summary>
    /// How many of this application's files were copied into <see cref="BuiltFrontendDirectory"/>
    /// (the compressed copies, the manifest and the <c>.gitignore</c> not counted); 0 when no
    /// backend project folder was given.
    /// </summary>
    public int BuiltFileCount { get; }
}
