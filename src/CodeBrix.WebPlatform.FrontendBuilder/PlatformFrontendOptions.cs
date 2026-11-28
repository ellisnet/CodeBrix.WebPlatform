using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Describes a platform application's frontend to <see cref="PlatformFrontend"/>: where it is,
/// which backend its client code is generated from, and how it is built. Only
/// <see cref="FrontendDirectory"/> and one of <see cref="BackendProjectPath"/> /
/// <see cref="BackendAssemblyPath"/> are required. A platform application with several frontend
/// applications lists them in <see cref="Applications"/> instead of setting
/// <see cref="FrontendDirectory"/>; they share the backend and everything else here.
/// <para>
/// The folder layout of a platform frontend is fixed: the generated client code in
/// <c>src/generated</c>, the shared crate in <c>platform/codebrix-webplatform</c>, the platform's
/// crates in <c>platform/vendor</c> (the first application's, when there are several), the cargo
/// configuration in <c>.cargo</c>, dx's own data in <c>target/dx-home</c>, the Tailwind input
/// <c>tailwind.css</c> and the generated stylesheet <c>assets/generated/tailwind.css</c>; the
/// built site goes into the backend project's <c>frontend-built</c> folder. None of these can be
/// moved. Every folder of that list that the build owns holds a <c>.gitignore</c> the build
/// writes, so git ignores it without a rule of the developer's.
/// </para>
/// </summary>
public sealed class PlatformFrontendOptions
{
    /// <summary>
    /// The application's frontend folder: the Rust crate with its <c>Cargo.toml</c> and
    /// <c>Dioxus.toml</c>. Required. A relative path is taken from the current directory.
    /// </summary>
    public string FrontendDirectory { get; set; }

    /// <summary>
    /// The name of the application in <see cref="FrontendDirectory"/>: lowercase letters, digits
    /// and hyphens. A named application is served below its name (<c>https://host/sales</c> for
    /// "sales"): dx builds it with that base path, and <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/>
    /// puts its site into the sub-folder of that name in the built frontend folder. <c>null</c>
    /// (the default) or empty: the application has no name (it is the root application) and is
    /// served at the site root.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// The frontend applications of a platform application that has several, first one first.
    /// Empty by default: the one application is <see cref="FrontendDirectory"/>. When it has
    /// entries, <see cref="FrontendDirectory"/> and <see cref="ApplicationName"/> must be left
    /// unset. Every application has a frontend folder of its own; at most one has no name (it
    /// is served at the site root) and no name is used twice.
    /// <see cref="PlatformFrontend.MaterializeAsync"/> and <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/>
    /// work on all of them.
    /// </summary>
    public IList<FrontendApplicationOptions> Applications { get; } = new List<FrontendApplicationOptions>();

    /// <summary>
    /// Whether the frontend is built with bundle splitting (dx's <c>--wasm-split</c>): the
    /// WebAssembly is cut into a main file and pieces that the browser loads when they are first
    /// needed, so a visitor does not download the code of pages they never open. <c>null</c>
    /// (the default) and <c>false</c>: off. The frontend's own code decides what is split off:
    /// its <c>Cargo.toml</c> switches on the <c>wasm-split</c> feature of <c>dioxus</c> and its
    /// router or its functions are marked for lazy loading; with nothing marked, a split build
    /// is one file as before. A split build always runs wasm-opt, also for a debug build, and
    /// <see cref="Shrink"/> is ignored for it. With several applications
    /// (<see cref="Applications"/>) each sets its own; this one is then the setting of the
    /// first application when that one sets none.
    /// </summary>
    public bool? WasmSplit { get; set; }

    /// <summary>
    /// The default of <see cref="SharedChunkThreshold"/>: 16 KiB of code and data before
    /// optimisation.
    /// </summary>
    public const int DefaultSharedChunkThreshold = 16 * 1024;

    /// <summary>The default of <see cref="SharedChunkLimit"/>: 16 shared chunks, the common chunk included.</summary>
    public const int DefaultSharedChunkLimit = 16;

    /// <summary>
    /// With bundle splitting (<see cref="WasmSplit"/>): from what size a group of code and data
    /// that several pieces share gets a shared chunk of its own, in bytes. The shared code is
    /// grouped by the exact set of pieces that use it; a group of at least this size becomes a
    /// chunk that only those pieces fetch, and a smaller group goes into the chunk of a larger
    /// group that all of its pieces use too, or else into the common chunk. The size is measured
    /// BEFORE optimisation: the files are shrunk afterwards, and the Brotli copy of a chunk is
    /// roughly a third of it. <c>null</c> (the default): <see cref="DefaultSharedChunkThreshold"/>.
    /// At least 1. For ONE chunk with all the shared code, set <see cref="SharedChunkLimit"/> to
    /// 1. With several applications (<see cref="Applications"/>) this is the value of every
    /// application that sets none of its own.
    /// </summary>
    public int? SharedChunkThreshold { get; set; }

    /// <summary>
    /// With bundle splitting (<see cref="WasmSplit"/>): the most shared chunks a build makes, the
    /// common chunk included. When more groups reach <see cref="SharedChunkThreshold"/>, the
    /// largest keep their own chunks and the others are grouped as the smaller ones are.
    /// <c>null</c> (the default): <see cref="DefaultSharedChunkLimit"/>. At least 1; 1 gives one
    /// chunk with all the shared code. With several applications this is the value of every
    /// application that sets none of its own.
    /// </summary>
    public int? SharedChunkLimit { get; set; }

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
    /// Whether <see cref="PlatformFrontend.MaterializeAsync"/> writes the generated Tailwind
    /// stylesheet when it is missing, so that plain <c>cargo check/test</c> work on a fresh
    /// clone (the application's Rust names that file as an asset, and dx normally writes it during
    /// a build). <c>true</c> by default. It starts the packaged Tailwind program
    /// (<see cref="Tools.PackagedTailwind"/>) once (<c>tailwindcss -i tailwind.css -o assets/generated/tailwind.css</c>
    /// in the frontend folder), only when the input stylesheet exists and the output does not; an
    /// existing output is never touched. A missing packaged program does not fail materializing:
    /// the result says so.
    /// </summary>
    public bool GenerateMissingStylesheet { get; set; } = true;

    /// <summary>
    /// Whether the WebAssembly file is shrunk by wasm-opt. <c>null</c> (the default): a release
    /// build is shrunk (dx runs wasm-opt) and a debug build is not. <c>true</c> also shrinks a
    /// debug build (this library runs wasm-opt on dx's output). <c>false</c> also leaves a release
    /// build unshrunk: dx is told to skip wasm-opt (the platform's dx reads the environment
    /// variable <c>DX_SKIP_WASM_OPT</c>), so no wasm-opt is needed. With bundle splitting
    /// (<see cref="WasmSplit"/>) dx shrinks regardless and this setting is ignored.
    /// <see cref="FrontendBuildResult.Shrunk"/> says what happened.
    /// </summary>
    public bool? Shrink { get; set; }

    /// <summary>
    /// Whether the shipped WebAssembly keeps its function names (a <c>name</c> section), for a
    /// PROFILING build: a browser's CPU profile then shows the Rust functions instead of
    /// <c>wasm-function[N]</c>, and the size report (<see cref="FrontendBuildResult.Sizes"/>)
    /// breaks each file's code down by crate. <c>false</c> (the default): no names, as always.
    /// <c>true</c>: dx is started with <c>--keep-names</c>, and a release build with
    /// <c>--debug-symbols true</c> instead of <c>--debug-symbols false</c>, so wasm-opt keeps
    /// the names (it keeps the debug information the files carry with them too, which makes
    /// them larger); a debug build this library shrinks (<see cref="Shrink"/> <c>true</c>) is
    /// shrunk with <c>--debuginfo</c> instead of <c>--strip-debug</c>. Every application of the
    /// build is built this way. Do not ship such a build.
    /// </summary>
    public bool KeepFunctionNames { get; set; }

    /// <summary>
    /// The compressed copies <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/>
    /// writes into the built frontend folder (<c>frontend-built</c>) once it has filled it:
    /// beside each file of the built applications whose extension is in
    /// <see cref="CompressibleExtensions"/> a <c>&lt;name&gt;.br</c> and/or <c>&lt;name&gt;.gz</c>
    /// copy at the strongest setting, only when the copy is smaller than the file. The platform
    /// backend serves a copy to a browser that accepts it. <c>null</c> (the default): Brotli and
    /// gzip (<see cref="FrontendCompression.BrotliAndGzip"/>) for a release build and none
    /// (<see cref="FrontendCompression.None"/>) for a debug build; set, that value for both.
    /// Nothing is compressed when the site is not copied into a backend project, nor by
    /// <see cref="PlatformFrontend.ServeAsync(string, System.Threading.CancellationToken)"/>.
    /// <see cref="FrontendBuildResult.Compression"/> says what was written.
    /// </summary>
    public FrontendCompression? CompressOutput { get; set; }

    /// <summary>
    /// The extensions (with the dot, any case) of the built files that get compressed copies
    /// (see <see cref="CompressOutput"/>). Pre-filled with
    /// <see cref="DefaultCompressibleExtensions"/>; add or remove entries to change it. Formats
    /// that are compressed already (PNG, JPEG, WOFF, WOFF2, archives and the like) gain nothing
    /// and are not in the defaults.
    /// </summary>
    public ISet<string> CompressibleExtensions { get; } = new HashSet<string>(DefaultCompressibleExtensions, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The platform's default <see cref="CompressibleExtensions"/>: what a built platform site
    /// holds that is not compressed already - the WebAssembly, the JavaScript glue and loader,
    /// stylesheets, pages, JSON and source maps, SVG, text, the plain-text formats, uncompressed
    /// fonts, shaders and the 3D and image data an application may ship as assets (glTF and its
    /// binary forms, Radiance HDR).
    /// </summary>
    public static IReadOnlyList<string> DefaultCompressibleExtensions { get; } = new[]
    {
        ".wasm", ".js", ".mjs", ".css", ".html", ".htm", ".json", ".map", ".webmanifest",
        ".svg", ".txt", ".xml", ".csv", ".md", ".toml", ".yaml", ".yml",
        ".wgsl", ".glsl", ".gltf", ".glb", ".bin", ".obj", ".hdr", ".ttf", ".otf",
    };

    /// <summary>
    /// The frontend's cargo features to switch on for <see cref="PlatformFrontend.ServeAsync(string, System.Threading.CancellationToken)"/>,
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
    /// the frontend folder. Build output: never edited, ignored by git through its own
    /// <c>.gitignore</c>. <c>null</c> until <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string GeneratedClientDirectory
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), "src", "generated");

    /// <summary>
    /// Where the platform's shared Rust crate is written: <c>platform/codebrix-webplatform</c>
    /// inside the frontend folder, which the frontend's <c>Cargo.toml</c> names as a path
    /// dependency. Build output: never edited, ignored by git through its own <c>.gitignore</c>.
    /// <c>null</c> until <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string SharedCrateDirectory
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), "platform", SharedRustCrate.CrateName);

    /// <summary>
    /// The folder the platform's crates are written into for
    /// <see cref="Crates.CrateMode.PlatformCratesOnly"/>: <c>platform/vendor</c> inside the
    /// frontend folder - of the first application in <see cref="Applications"/>, when that list
    /// is used (the applications share the one folder). Build output: never edited, ignored by
    /// git through its own <c>.gitignore</c>. <c>null</c> until a frontend folder is set.
    /// </summary>
    public string VendorDirectory
    {
        get
        {
            var frontend = !string.IsNullOrWhiteSpace(FrontendDirectory)
                ? FrontendDirectory
                : Applications.Count > 0 ? Applications[0]?.FrontendDirectory : null;
            return string.IsNullOrWhiteSpace(frontend) ? null : Path.Combine(Path.GetFullPath(frontend), "platform", "vendor");
        }
    }

    /// <summary>
    /// The folder dx keeps its own data in (its <c>DX_HOME</c>): <c>target/dx-home</c> inside
    /// the frontend folder. It belongs to the build, so dx never uses a program that some other
    /// dx once downloaded into the user's own dx folder. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string DxHomeDirectory => InFrontend(Path.Combine("target", "dx-home"));

    /// <summary>
    /// The Tailwind input stylesheet: <c>tailwind.css</c> in the frontend folder, where dx looks
    /// for it. The developer's own file. <c>null</c> until <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string TailwindInputFile => InFrontend("tailwind.css");

    /// <summary>
    /// The generated Tailwind stylesheet: <c>assets/generated/tailwind.css</c> in the frontend
    /// folder, where the platform's dx writes it (the frontend names it as an asset,
    /// <c>asset!("/assets/generated/tailwind.css")</c>). Build output: the folder
    /// <c>assets/generated</c> holds its own <c>.gitignore</c>. <c>null</c> until
    /// <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string TailwindOutputFile => InFrontend(Path.Combine("assets", "generated", "tailwind.css"));

    /// <summary>
    /// The cargo configuration <see cref="PlatformFrontend.MaterializeAsync"/> writes:
    /// <c>.cargo/config.toml</c> inside the frontend folder, which tells cargo where the crates
    /// come from. Build output: never edited; the <c>.cargo</c> folder is ignored by git through
    /// its own <c>.gitignore</c>. <c>null</c> until <see cref="FrontendDirectory"/> is set.
    /// </summary>
    public string CargoConfigurationFile
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), ".cargo", "config.toml");

    private string InFrontend(string path)
        => string.IsNullOrWhiteSpace(FrontendDirectory) ? null : Path.Combine(Path.GetFullPath(FrontendDirectory), path);
}
