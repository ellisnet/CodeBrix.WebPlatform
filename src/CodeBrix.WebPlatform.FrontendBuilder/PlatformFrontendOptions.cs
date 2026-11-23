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
    /// "sales"): dx builds it with that base path, and <see cref="PlatformFrontend.BuildAsync"/>
    /// puts its site into the sub-folder of that name in the web root. <c>null</c> (the
    /// default) or empty: the application has no name and is served at the site root.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// The frontend applications of a platform application that has several, first one first.
    /// Empty by default: the one application is <see cref="FrontendDirectory"/>. When it has
    /// entries, <see cref="FrontendDirectory"/> and <see cref="ApplicationName"/> must be left
    /// unset. Every application has a frontend folder of its own; at most one has no name (it
    /// is served at the site root) and no name is used twice.
    /// <see cref="PlatformFrontend.MaterializeAsync"/> and <see cref="PlatformFrontend.BuildAsync"/>
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
    /// <c>platform/vendor</c> inside the frontend folder (the first application's, when there
    /// are several: they share the one folder). The folder belongs to the platform's
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
    /// Whether <see cref="PlatformFrontend.MaterializeAsync"/> writes the generated Tailwind
    /// stylesheet when it is missing, so that plain <c>cargo check/test</c> work on a fresh
    /// clone (the application's Rust names that file as an asset, and dx normally writes it during
    /// a build). <c>true</c> by default. It starts the packaged Tailwind program
    /// (<see cref="Tools.PackagedTailwind"/>) once (<c>tailwindcss -i &lt;input&gt; -o &lt;output&gt;</c>
    /// in the frontend folder), only when the input stylesheet exists and the output does not; an
    /// existing output is never touched. A missing packaged program does not fail materializing:
    /// the result says so.
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
    /// (<see cref="WasmSplit"/>) dx shrinks regardless and this setting is ignored.
    /// <see cref="FrontendBuildResult.Shrunk"/> says what happened.
    /// </summary>
    public bool? Shrink { get; set; }

    /// <summary>
    /// The compressed copies <see cref="PlatformFrontend.BuildAsync"/> writes into the web root
    /// once it has filled it: beside each compressible file of the built applications (the
    /// WebAssembly, JavaScript, stylesheets, HTML, JSON, SVG, text and the like - never images,
    /// fonts or archives that are compressed already) a <c>&lt;name&gt;.br</c> and/or
    /// <c>&lt;name&gt;.gz</c> copy at the strongest setting, only when the copy is smaller than
    /// the file. The platform backend serves a copy to a browser that accepts it. Default
    /// <see cref="FrontendCompression.BrotliAndGzip"/>. Nothing is compressed without a web root,
    /// nor by <see cref="PlatformFrontend.ServeAsync(string, System.Threading.CancellationToken)"/>.
    /// <see cref="FrontendBuildResult.Compression"/> says what was written.
    /// </summary>
    public FrontendCompression CompressOutput { get; set; } = FrontendCompression.BrotliAndGzip;

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
    /// is set, else <c>platform/vendor</c> inside the frontend folder (of the first application
    /// in <see cref="Applications"/>, when that list is used). <c>null</c> until a frontend
    /// folder is set.
    /// </summary>
    public string ResolvedVendorDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(VendorDirectory))
            {
                return Path.GetFullPath(VendorDirectory);
            }

            var frontend = !string.IsNullOrWhiteSpace(FrontendDirectory)
                ? FrontendDirectory
                : Applications.Count > 0 ? Applications[0]?.FrontendDirectory : null;
            return string.IsNullOrWhiteSpace(frontend) ? null : Path.Combine(Path.GetFullPath(frontend), "platform", "vendor");
        }
    }

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
