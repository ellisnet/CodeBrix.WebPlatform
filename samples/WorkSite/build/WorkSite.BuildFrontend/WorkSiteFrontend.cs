using System.IO;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
using CodeBrix.WebPlatform.FrontendBuilder;

namespace WorkSite.BuildFrontend;

/// <summary>
/// WorkSite's settings for CodeBrix.WebPlatform.FrontendBuilder, which does the generic work:
/// the prerequisite check, materializing the generated client code and the shared crate into the
/// frontend, and running dx. What is particular to WorkSite is here: its paths and its type
/// mapping. (The folder layout of the frontend - where dx keeps its data, where the generated
/// Tailwind stylesheet goes - is the platform's and fixed. What the generated client code says about the backend itself - its request marker
/// and session refresh - the backend declares; nothing about it is set here.) dx and
/// wasm-bindgen come from the host-tools package for the machine the build program
/// runs on (WorkSite.BuildFrontend.csproj references all five, each for its own platform), and the
/// frontend uses only the platform's crates (the default crate mode). The generated code is
/// written with the platform's default options (the hub code calls the shared crate's SignalR
/// client).
/// </summary>
public static class WorkSiteFrontend
{
    /// <summary>The frontend's cargo feature that switches on hot reload for <c>serve</c>.</summary>
    public const string DevToolsFeature = "devtools";

    /// <summary>
    /// The .NET-to-Rust type mapping WorkSite generates its client code with. It is the
    /// platform's default mapping; this is the place to change it for the whole application, for
    /// example <c>types.Map&lt;decimal&gt;(new RustType("Money") { Import = "crate::money::Money" })</c>.
    /// </summary>
    /// <returns>The mapping.</returns>
    public static RustTypeMap CreateTypeMap() => new RustTypeMap();

    /// <summary>
    /// The options WorkSite's frontend is built with.
    /// </summary>
    /// <param name="paths">The WorkSite paths.</param>
    /// <param name="output">Where progress lines go.</param>
    /// <returns>The options.</returns>
    public static PlatformFrontendOptions CreateOptions(BuilderPaths paths, TextWriter output)
        => new PlatformFrontendOptions
        {
            FrontendDirectory = paths.Layout.Frontend,
            BackendProjectPath = paths.ApiProject,
            BackendAssemblyPath = paths.ApiAssembly,
            Types = CreateTypeMap(),
            ServeFeatures = { DevToolsFeature },
            Output = output,
        };
}
