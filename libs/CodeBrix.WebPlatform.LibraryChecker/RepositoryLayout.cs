using System;
using System.IO;

namespace CodeBrix.WebPlatform.LibraryChecker;

/// <summary>
/// The folders and files of the CodeBrix.WebPlatform repository that the maintainer tool checks,
/// all derived from the repository root (the folder that holds CodeBrix.WebPlatform.slnx and the
/// licence allowlist). Only what is in the repository outside <c>samples/</c> is checked:
/// the platform's crates that the FrontendBuilder package ships, the sources of the two
/// distributed build tools and their crates, the patches, the notices and the no-Node rule.
/// </summary>
public sealed class RepositoryLayout
{
    /// <summary>The file, relative to the repository root, that marks it (with the solution).</summary>
    public const string MarkerFile = "tools/licensing/license-allowlist.json";

    /// <summary>The solution file at the repository root.</summary>
    public const string SolutionFile = "CodeBrix.WebPlatform.slnx";

    /// <summary>
    /// Creates a layout for a known root folder.
    /// </summary>
    /// <param name="root">The repository root.</param>
    public RepositoryLayout(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The repository root.</summary>
    public string Root { get; }

    /// <summary>The folder of the FrontendBuilder library's Rust material.</summary>
    public string PlatformRust => Combine("src/CodeBrix.WebPlatform.FrontendBuilder/Rust");

    /// <summary>The shared Rust crate's source.</summary>
    public string SharedCrate => Path.Combine(PlatformRust, "codebrix-webplatform");

    /// <summary>The shared crate's Cargo.lock (repository-only; serves its own <c>cargo test</c>).</summary>
    public string SharedCrateLock => Path.Combine(SharedCrate, "Cargo.lock");

    /// <summary>The manifest that names the platform's crate set (what <c>cargo vendor</c> takes).</summary>
    public string PlatformCrateSet => Path.Combine(PlatformRust, "platform-crates");

    /// <summary>The lock file of the platform's crate set.</summary>
    public string PlatformCrateSetLock => Path.Combine(PlatformCrateSet, "Cargo.lock");

    /// <summary>The platform's vendored crates, which the FrontendBuilder package ships as one archive.</summary>
    public string PlatformVendor => Path.Combine(PlatformRust, "vendor");

    /// <summary>The upgrade notes of the platform's crates chosen by name.</summary>
    public string PlatformVendoring => Path.Combine(PlatformRust, "vendoring");

    /// <summary>The folder of the platform crates' notices: the hand-written header and the generated file.</summary>
    public string PlatformNoticesFolder => Path.Combine(PlatformRust, "notices");

    /// <summary>The hand-written opening of the platform crates' notices.</summary>
    public string PlatformNoticesHeaderFile => Path.Combine(PlatformNoticesFolder, "notices-header-PlatformCrates.txt");

    /// <summary>The generated notices of the platform's crates, packed into the FrontendBuilder package.</summary>
    public string PlatformNoticesFile => Path.Combine(PlatformNoticesFolder, "THIRD-PARTY-NOTICES-PlatformCrates.txt");

    /// <summary>The folder that builds the distributed <c>dx</c> and <c>wasm-bindgen</c>.</summary>
    public string HostToolsFolder => Combine("tools/dx-native-tools");

    /// <summary>The tool sources: the two CLIs, their vendored crates and the cargo configuration.</summary>
    public string ToolSources => Path.Combine(HostToolsFolder, "sources");

    /// <summary>The tool sources, relative to the repository root, with forward slashes.</summary>
    public string ToolSourcesRelative => Relative(ToolSources);

    /// <summary>The vendored crates the two tools build from.</summary>
    public string ToolingVendor => Path.Combine(ToolSources, "vendor");

    /// <summary>The build tools' vendor folder, relative to the repository root, with forward slashes.</summary>
    public string ToolingVendorRelative => Relative(ToolingVendor);

    /// <summary>The patched Dioxus CLI source.</summary>
    public string DioxusCli => Path.Combine(ToolSources, "dioxus-cli");

    /// <summary>
    /// The repository's declarations-only stand-in for the <c>libgit2-sys</c> crate, which the
    /// Dioxus CLI's Cargo.toml substitutes for the crates.io crate (that one carries libgit2's C
    /// source).
    /// </summary>
    public string Libgit2StandIn => Path.Combine(ToolSources, "codebrix-libgit2-sys");

    /// <summary>The wasm-bindgen CLI source.</summary>
    public string WasmBindgenCli => Path.Combine(ToolSources, "wasm-bindgen-cli");

    /// <summary>The tools' records: per tool its pristine crates.io archive, checksum, patches and notes.</summary>
    public string ToolVendoring => Path.Combine(HostToolsFolder, "vendoring");

    /// <summary>The tool records, relative to the repository root, with forward slashes.</summary>
    public string ToolVendoringRelative => Relative(ToolVendoring);

    /// <summary>
    /// The folder that holds the third-party notices of the distributed build tools (<c>dx</c> and
    /// <c>wasm-bindgen</c>) and of the tool sources: the hand-written openings and the generated
    /// files, which the native build copies beside every built program.
    /// </summary>
    public string HostToolsNoticesFolder => Path.Combine(HostToolsFolder, "notices");

    /// <summary>The generated record of every crate vendored for the tools (repository-only).</summary>
    public string ToolSourcesNoticesFile => Path.Combine(HostToolsNoticesFolder, "THIRD-PARTY-NOTICES-ToolSources.txt");

    /// <summary>The hand-written opening of <see cref="ToolSourcesNoticesFile"/>.</summary>
    public string ToolSourcesNoticesHeaderFile => Path.Combine(HostToolsNoticesFolder, "notices-header-ToolSources.txt");

    /// <summary>The licence policy folder.</summary>
    public string Licensing => Combine("tools/licensing");

    /// <summary>The licence allowlist that only the repository owner edits.</summary>
    public string AllowlistFile => Combine(MarkerFile);

    /// <summary>The sample applications, which this tool does not check.</summary>
    public string Samples => Combine("samples");

    /// <summary>The committed native programs (one folder per runtime identifier).</summary>
    public string NativeLibraries => Combine("native_libraries");

    /// <summary>The local package feed (git-ignored).</summary>
    public string TempPackages => Combine("temp-packages");

    /// <summary>The native build's disposable output (git-ignored).</summary>
    public string HostToolsOutput => Path.Combine(HostToolsFolder, "output");

    /// <summary>
    /// True when a folder is the repository root.
    /// </summary>
    /// <param name="directory">The folder.</param>
    public static bool IsRoot(string directory)
        => !string.IsNullOrWhiteSpace(directory)
            && File.Exists(Path.Combine(directory, SolutionFile))
            && File.Exists(Path.Combine(directory, MarkerFile.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// Finds the repository root by walking up from <paramref name="start"/>.
    /// </summary>
    /// <param name="start">Where to start; a file path is reduced to its folder.</param>
    /// <returns>The layout, or <c>null</c> when no repository root was found.</returns>
    public static RepositoryLayout Find(string start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory != null; directory = directory.Parent)
        {
            if (IsRoot(directory.FullName))
            {
                return new RepositoryLayout(directory.FullName);
            }
        }

        return null;
    }

    /// <summary>A path below the root, relative to it with forward slashes.</summary>
    /// <param name="path">A full path.</param>
    public string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private string Combine(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
}
