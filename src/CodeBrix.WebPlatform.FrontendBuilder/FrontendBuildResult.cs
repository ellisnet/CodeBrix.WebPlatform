using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>What <see cref="PlatformFrontend.BuildAsync"/> built.</summary>
public sealed class FrontendBuildResult
{
    internal FrontendBuildResult(FrontendConfiguration configuration, string outputDirectory, bool shrunk, WasmOptSource wasmOpt, HostTools tools, string webRootDirectory, int webRootFileCount)
    {
        Configuration = configuration;
        OutputDirectory = outputDirectory;
        Shrunk = shrunk;
        WasmOpt = wasmOpt;
        Tools = tools;
        WebRootDirectory = webRootDirectory;
        WebRootFileCount = webRootFileCount;
    }

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

    /// <summary>The web-root folder the site was copied into, or <c>null</c> when none was named.</summary>
    public string WebRootDirectory { get; }

    /// <summary>How many files were copied into <see cref="WebRootDirectory"/>; 0 when none was named.</summary>
    public int WebRootFileCount { get; }
}
