using System.Collections.Generic;
using System.IO;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Describes one platform application's frontend to <see cref="PlatformFrontend"/>: where it is,
/// which backend its client code is generated from, and how it is built. Only
/// <see cref="FrontendDirectory"/> and one of <see cref="BackendProjectPath"/> /
/// <see cref="BackendAssemblyPath"/> are required.
/// </summary>
public sealed class PlatformFrontendOptions
{
    /// <summary>
    /// The application's frontend folder: the Rust crate with its <c>Cargo.toml</c> and
    /// <c>Dioxus.toml</c>. Required. A relative path is taken from the current directory.
    /// </summary>
    public string FrontendDirectory { get; set; }

    /// <summary>
    /// The backend's project file (<c>.csproj</c>). When it is set,
    /// <see cref="PlatformFrontend.MaterializeAsync"/> builds it in the Release configuration
    /// before reading it.
    /// </summary>
    public string BackendProjectPath { get; set; }

    /// <summary>
    /// The compiled backend assembly that the client code is generated from, for example
    /// <c>backend/MyApp.Api/bin/Release/net10.0/MyApp.Api.dll</c>. When it is not set, the
    /// assembly the Release build of <see cref="BackendProjectPath"/> produces is used. Set only
    /// this (and not the project) to read an assembly that is already built.
    /// </summary>
    public string BackendAssemblyPath { get; set; }

    /// <summary>
    /// The .NET-to-Rust type mapping for the whole application. Starts as the platform's
    /// defaults; change it with <see cref="RustTypeMap.Map(string, RustType)"/>.
    /// </summary>
    public RustTypeMap Types { get; set; } = new RustTypeMap();

    /// <summary>
    /// How the client code is written (the SignalR module path, templates and so on). Starts as
    /// BackendMapper's defaults.
    /// </summary>
    public RustClientOptions ClientOptions { get; set; } = new RustClientOptions();

    /// <summary>
    /// Where the frontend's crates come from: the platform's crates only, offline (the default),
    /// or crates.io because the application adds crates of its own. See <see cref="Crates.CrateMode"/>.
    /// </summary>
    public CrateMode CrateMode { get; set; } = CrateMode.PlatformCratesOnly;

    /// <summary>
    /// Where <see cref="PlatformFrontend.MaterializeAsync"/> writes the platform's crates for
    /// <see cref="Crates.CrateMode.PlatformCratesOnly"/>, or <c>null</c> for the default,
    /// <c>platform/vendor</c> inside the frontend folder. The folder belongs to the platform's
    /// crates: build output, never edited, ignored by git. A relative path is taken from the
    /// current directory.
    /// </summary>
    public string VendorDirectory { get; set; }

    /// <summary>
    /// The <c>dx</c> program to run when the build program has no host-tools package for this
    /// machine (see <see cref="Tools.HostTools"/>). Set together with <see cref="WasmBindgenPath"/>.
    /// </summary>
    public string DxPath { get; set; }

    /// <summary>
    /// The <c>wasm-bindgen</c> program to run when the build program has no host-tools package
    /// for this machine. Set together with <see cref="DxPath"/>.
    /// </summary>
    public string WasmBindgenPath { get; set; }

    /// <summary>
    /// The folder dx keeps its own data in (its <c>DX_HOME</c>). It belongs to the build, so dx
    /// never uses a program that some other dx once downloaded into the user's own dx folder.
    /// When it is not set, <c>target/dx-home</c> inside the frontend folder is used.
    /// </summary>
    public string DxHomeDirectory { get; set; }

    /// <summary>
    /// The oldest release of the standalone Tailwind program to accept, as "major.minor.patch".
    /// Starts as <see cref="FrontendRequirements.MinimumTailwindVersion"/>.
    /// </summary>
    public string MinimumTailwindVersion { get; set; } = FrontendRequirements.MinimumTailwindVersion;

    /// <summary>
    /// Whether <see cref="PlatformFrontend.MaterializeAsync"/> writes the generated Tailwind
    /// stylesheet when it is missing, so that plain <c>cargo check/test</c> work on a fresh
    /// clone (the application's Rust names that file as an asset, and dx normally writes it during
    /// a build). <c>true</c> by default. It starts the installed Tailwind program once
    /// (<c>tailwindcss -i &lt;input&gt; -o &lt;output&gt;</c> in the frontend folder), only when the
    /// input stylesheet exists and the output does not; an existing output is never touched. A
    /// missing or too old Tailwind does not fail materializing: the result says so.
    /// </summary>
    public bool GenerateMissingStylesheet { get; set; } = true;

    /// <summary>
    /// The frontend's Tailwind input stylesheet, or <c>null</c> for the default,
    /// <c>tailwind.css</c> in the frontend folder (where dx looks for it). A relative path is
    /// taken from the frontend folder.
    /// </summary>
    public string TailwindInputFile { get; set; }

    /// <summary>
    /// The generated Tailwind stylesheet, or <c>null</c> for the default,
    /// <c>assets/tailwind.css</c> in the frontend folder (where dx writes it). A relative path is
    /// taken from the frontend folder.
    /// </summary>
    public string TailwindOutputFile { get; set; }

    /// <summary>
    /// Whether the WebAssembly file is shrunk by wasm-opt. <c>null</c> (the default): a release
    /// build is shrunk (dx runs wasm-opt) and a debug build is not. <c>true</c> also shrinks a
    /// debug build (this library runs wasm-opt on dx's output). <c>false</c> also leaves a release
    /// build unshrunk: dx is told to skip wasm-opt (the platform's dx reads the environment
    /// variable <c>DX_SKIP_WASM_OPT</c>), so no wasm-opt is needed. With bundle splitting
    /// (<c>--wasm-split</c>, not used by <see cref="PlatformFrontend.BuildAsync"/>) dx would shrink
    /// regardless. <see cref="FrontendBuildResult.Shrunk"/> says what happened.
    /// </summary>
    public bool? Shrink { get; set; }

    /// <summary>
    /// The frontend's cargo features to switch on for <see cref="PlatformFrontend.ServeAsync"/>,
    /// typically the one that turns on <c>dioxus/devtools</c> for hot reload. Empty by default.
    /// </summary>
    public IList<string> ServeFeatures { get; } = new List<string>();

    /// <summary>
    /// Where progress lines (and the output of <c>dx serve</c>) are written, or <c>null</c> for
    /// nowhere.
    /// </summary>
    public TextWriter Output { get; set; }

    /// <summary>
    /// Where the client code generated from the backend is written: <c>src/generated</c> inside
    /// the frontend folder. Build output: never edited, ignored by git. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string GeneratedClientDirectory
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), "src", "generated");

    /// <summary>
    /// Where the platform's shared Rust crate is written: <c>platform/codebrix-webplatform</c>
    /// inside the frontend folder, which the frontend's <c>Cargo.toml</c> names as a path
    /// dependency. Build output: never edited, ignored by git. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string SharedCrateDirectory
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), "platform", SharedRustCrate.CrateName);

    /// <summary>
    /// The folder the platform's crates are written into: <see cref="VendorDirectory"/> when it
    /// is set, else <c>platform/vendor</c> inside the frontend folder. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string ResolvedVendorDirectory
        => !string.IsNullOrWhiteSpace(VendorDirectory)
            ? Path.GetFullPath(VendorDirectory)
            : string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), "platform", "vendor");

    /// <summary>
    /// The Tailwind input stylesheet: <see cref="TailwindInputFile"/>, or <c>tailwind.css</c> in
    /// the frontend folder. <c>null</c> until <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string ResolvedTailwindInputFile => InFrontend(TailwindInputFile, "tailwind.css");

    /// <summary>
    /// The generated Tailwind stylesheet: <see cref="TailwindOutputFile"/>, or
    /// <c>assets/tailwind.css</c> in the frontend folder. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string ResolvedTailwindOutputFile => InFrontend(TailwindOutputFile, Path.Combine("assets", "tailwind.css"));

    /// <summary>
    /// The cargo configuration <see cref="PlatformFrontend.MaterializeAsync"/> writes:
    /// <c>.cargo/config.toml</c> inside the frontend folder, which tells cargo where the crates
    /// come from. Build output: never edited, ignored by git. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string CargoConfigurationFile
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), ".cargo", "config.toml");

    private string InFrontend(string path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(FrontendDirectory))
        {
            return null;
        }

        var frontend = Path.GetFullPath(FrontendDirectory);
        return Path.GetFullPath(Path.Combine(frontend, string.IsNullOrWhiteSpace(path) ? fallback : path));
    }
}
