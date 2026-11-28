using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Sizes;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>
/// One REAL build of a small Dioxus frontend with the dx adopted for this machine
/// (native_libraries/&lt;rid&gt;/), the platform's crates and the packaged Tailwind, into a backend
/// project folder. OPT-IN: dx compiles Dioxus for WebAssembly (minutes), so the tests run only
/// when the environment variable <see cref="OptInVariable"/> is "1"; otherwise they are skipped
/// with the reason. The builds are made once for the class (<see cref="BuiltFrontendFixture"/>,
/// and <see cref="SplitFrontendFixture"/> for bundle splitting). <see cref="DxFolderVariable"/>
/// names another folder with dx and wasm-bindgen to build with, for a dx not yet adopted.
/// </summary>
public class PlatformFrontendDxBuildTests : IClassFixture<PlatformFrontendDxBuildTests.BuiltFrontendFixture>, IClassFixture<PlatformFrontendDxBuildTests.SplitFrontendFixture>
{
    /// <summary>The variable that switches these tests on.</summary>
    public const string OptInVariable = "CODEBRIX_WEBPLATFORM_DX_BUILD_TESTS";

    /// <summary>
    /// The variable that names a folder holding the dx and wasm-bindgen to build with instead of
    /// the adopted ones (native_libraries/&lt;rid&gt;/): a freshly built dx before it is adopted.
    /// </summary>
    public const string DxFolderVariable = "CODEBRIX_WEBPLATFORM_DX_BUILD_TESTS_DX_FOLDER";

    private readonly BuiltFrontendFixture _built;

    private readonly SplitFrontendFixture _split;

    public PlatformFrontendDxBuildTests(BuiltFrontendFixture built, SplitFrontendFixture split)
    {
        _built = built;
        _split = split;
    }

    private void SkipUnlessSplitBuilt()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(OptInVariable) == "1", $"A real dx build takes minutes; set {OptInVariable}=1 to run it.");
        Assert.SkipWhen(_split.SkipReason != null, _split.SkipReason ?? string.Empty);
        if (_split.Failure != null)
        {
            if (_split.Failure.Message.Contains("the dx program does not match this library", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The split build tests need a dx with the shared-chunk options (dioxus-cli patch 0011 and splitter patch 0007): the dx they ran with does not report its shared chunks, so it predates them. "
                    + $"Adopt the rebuilt dx into native_libraries/, or name its folder in {DxFolderVariable}. dx said: " + _split.Failure.Message,
                    _split.Failure);
            }

            throw new InvalidOperationException("The split build failed: " + _split.Failure.Message, _split.Failure);
        }
    }

    private void SkipUnlessBuilt()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(OptInVariable) == "1", $"A real dx build takes minutes; set {OptInVariable}=1 to run it.");
        Assert.SkipWhen(_built.SkipReason != null, _built.SkipReason ?? string.Empty);
        if (_built.Failure != null)
        {
            throw new InvalidOperationException("The build failed: " + _built.Failure.Message, _built.Failure);
        }
    }

    [Fact]
    public void BuildAsync_writes_the_site_into_frontend_built_of_the_backend_project_folder()
    {
        SkipUnlessBuilt();

        //Arrange
        var built = _built.Folder.Combine("backend/" + BuiltFrontend.FolderName);

        //Assert
        _built.Result.BuiltFrontendDirectory.Should().Be(built);
        _built.Result.BuiltApplicationDirectory.Should().Be(built);
        _built.Result.BuiltFileCount.Should().BeGreaterThan(0);
        File.Exists(Path.Combine(built, "index.html")).Should().BeTrue();
        Directory.EnumerateFiles(built, "*.wasm", SearchOption.AllDirectories).Should().NotBeEmpty();
        File.ReadAllText(Path.Combine(built, FrontendManifest.FileName)).Should().Be(FrontendManifestWriter.Create(FrontendApplicationSet.SingleDefault));
        File.ReadAllText(Path.Combine(built, BuildOutputFolder.IgnoreFileName)).Should().Be(BuildOutputFolder.IgnoreFileText);
        Directory.Exists(_built.Folder.Combine("backend/wwwroot")).Should().BeFalse("the build never writes the developer's wwwroot");
        _built.Result.Compression.Compression.Should().Be(FrontendCompression.None, "a debug build is not compressed by default");
        Directory.EnumerateFiles(built, "*.br", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void BuildAsync_leaves_the_target_folder_ignoring_itself()
    {
        SkipUnlessBuilt();

        //Assert
        File.ReadAllText(_built.Folder.Combine("frontend/target/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
        File.Exists(_built.Folder.Combine("frontend/.gitignore")).Should().BeFalse();
    }

    /// <summary>
    /// NEEDS THE PATCHED dx (brief 3.1: dx's default Tailwind output assets/generated/tailwind.css).
    /// With a dx that still writes assets/tailwind.css this fails: the class added after
    /// materializing is missing from assets/generated/tailwind.css, and assets/tailwind.css
    /// appears in the developer's assets folder.
    /// </summary>
    [Fact]
    public void the_dx_build_writes_the_tailwind_stylesheet_into_assets_generated_and_nothing_into_assets()
    {
        SkipUnlessBuilt();

        //Assert
        File.ReadAllText(_built.Folder.Combine("frontend/assets/generated/tailwind.css")).Should().Contain(".text-fuchsia-900", "dx regenerates the stylesheet during the build");
        File.ReadAllText(_built.Folder.Combine("frontend/assets/generated/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
        Directory.GetFileSystemEntries(_built.Folder.Combine("frontend/assets")).Select(Path.GetFileName).Should().Equal("generated");
    }

    /// <summary>
    /// dx runs Tailwind before it compiles, and Tailwind scans every folder that does not ignore
    /// itself: BuildAsync writes target/.gitignore BEFORE dx starts, so what lies in target/ never
    /// reaches the stylesheet (the fixture removes that file after materializing and puts a class
    /// name into target/).
    /// </summary>
    [Fact]
    public void BuildAsync_keeps_whatever_lies_in_the_target_folder_out_of_the_stylesheet()
    {
        SkipUnlessBuilt();

        //Act
        var css = Directory.EnumerateFiles(_built.Result.BuiltApplicationDirectory, "tailwind*.css", SearchOption.AllDirectories).Single();

        //Assert
        File.ReadAllText(css).Should().Contain(".text-fuchsia-900");
        File.ReadAllText(css).Should().NotContain(".text-lime-300", "target/ ignores itself before dx runs Tailwind");
        File.ReadAllText(_built.Folder.Combine("frontend/target/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void BuildAsync_reports_the_compiler_warnings_that_dx_only_logs()
    {
        SkipUnlessBuilt();

        //Assert
        _built.Result.CompilerWarnings.Should().ContainSingle()
            .Which.Should().StartWith("warning: function `unused_helper` is never used\n");
        _built.Result.CompilerWarnings[0].Should().Contain("src/main.rs");
        _built.Result.CompilerWarnings[0].Should().NotContain("\u001b");
    }

    [Fact]
    public void BuildAsync_reports_the_size_of_the_webassembly_without_names_by_default()
    {
        SkipUnlessBuilt();

        //Act
        var main = _built.Result.Sizes.Files.Single();

        //Assert
        main.Kind.Should().Be(WasmFileKind.Main);
        main.Bytes.Should().Be(new FileInfo(Path.Combine(_built.Result.BuiltApplicationDirectory, main.Path)).Length);
        main.CodeBytes.Should().BeGreaterThan(0);
        main.HasFunctionNames.Should().BeFalse();
        main.Crates.Should().BeEmpty();
    }

    [Fact]
    public void KeepFunctionNames_builds_webassembly_whose_code_the_size_report_breaks_down_by_crate()
    {
        SkipUnlessBuilt();

        //Act
        var main = _built.NamesResult.Sizes.Files.Single();

        //Assert
        main.HasFunctionNames.Should().BeTrue();
        main.Crates.Select(c => c.Crate).Should().Contain("dioxus_core");
        main.Crates.Select(c => c.Crate).Should().Contain("dx_build_test");
        main.Crates.Sum(c => c.CodeBytes).Should().BeGreaterThan(main.CodeBytes / 2);
    }

    /// <summary>NEEDS THE PATCHED dx (dioxus-cli patch 0011, splitter patch 0007).</summary>
    [Fact]
    public void a_split_build_with_a_low_threshold_makes_several_shared_chunks_and_reports_them()
    {
        SkipUnlessSplitBuilt();

        //Act
        var chunks = ChunkFiles(_split.Low);

        //Assert
        _split.Low.WasmSplit.Should().BeTrue();
        chunks.Count.Should().BeGreaterThan(1, "with a threshold of one byte every set of pieces that shares code gets a chunk of its own");
        _split.Low.SharedChunks.Should().Be(chunks.Count, "dx reports every chunk it wrote");
        _split.Low.Sizes.Files.Count(f => f.Kind == WasmFileKind.SharedChunk && f.Path.Contains("-dxh", StringComparison.Ordinal)).Should().Be(chunks.Count);
        chunks.Should().Contain(name => name.StartsWith("chunk_0_", StringComparison.Ordinal));
    }

    /// <summary>NEEDS THE PATCHED dx (splitter patches 0003 and 0007).</summary>
    [Fact]
    public void a_split_build_with_several_shared_chunks_built_twice_gives_the_same_files_byte_for_byte()
    {
        SkipUnlessSplitBuilt();

        //Act
        var first = FileHashes(_split.Low.BuiltApplicationDirectory);
        var second = FileHashes(_split.LowAgain.BuiltApplicationDirectory);

        //Assert
        first.Count.Should().BeGreaterThan(0);
        second.Should().Equal(first);
    }

    /// <summary>NEEDS THE PATCHED dx (dioxus-cli patch 0011, splitter patch 0007).</summary>
    [Fact]
    public void a_split_build_with_a_shared_chunk_limit_of_one_makes_exactly_one_shared_chunk()
    {
        SkipUnlessSplitBuilt();

        //Assert
        ChunkFiles(_split.OneChunk).Should().ContainSingle().Which.Should().StartWith("chunk_0_");
        _split.OneChunk.SharedChunks.Should().Be(1);
    }

    /// <summary>
    /// The site of a build holds exactly that build's files: BuildAsync empties dx's output
    /// folder (target/dx) before dx starts - dx itself never cleans it - and replaces the
    /// application's part of the built frontend folder. Built after two builds with more chunks
    /// in the same frontend folder, and with stale files put into both folders.
    /// NEEDS THE PATCHED dx (dioxus-cli patch 0011).
    /// </summary>
    [Fact]
    public void a_split_build_ships_none_of_an_earlier_builds_chunks_pieces_or_loader()
    {
        SkipUnlessSplitBuilt();

        foreach (var folder in new[] { _split.OneChunk.OutputDirectory, _split.OneChunk.BuiltApplicationDirectory })
        {
            //Act
            var names = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();

            //Assert
            names.Should().NotContain(SplitFrontendFixture.StaleFiles, folder);
            names.Count(n => n.StartsWith("__wasm_split", StringComparison.Ordinal) && n.EndsWith(".js", StringComparison.Ordinal)).Should().Be(1, $"one loader in {folder}");
            names.Count(n => n.StartsWith("chunk_", StringComparison.Ordinal) && n.Contains("-dxh", StringComparison.Ordinal) && n.EndsWith(".wasm", StringComparison.Ordinal)).Should().Be(1, $"the one chunk of this build in {folder}");
        }
    }

    /// <summary>NEEDS THE PATCHED dx (dioxus-cli patch 0013).</summary>
    [Fact]
    public void a_split_build_names_each_piece_file_after_its_piece()
    {
        SkipUnlessSplitBuilt();

        //Act
        var pieces = Directory.EnumerateFiles(_split.Low.BuiltApplicationDirectory, "module_*-dxh*.wasm", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Select(name => name.Substring(name.IndexOf('_', "module_".Length) + 1).Split("-dxh")[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        //Assert
        pieces.Should().Equal("first", "lazy_LazyAlpha", "lazy_LazyBeta", "second", "third");
    }

    /// <summary>
    /// The chunk files the loader fetches: those dx gave its content hash. (A debug build also
    /// keeps dx's unhashed working copies in its wasm/ folder, which nothing requests.)
    /// </summary>
    private static List<string> ChunkFiles(FrontendBuildResult result)
        => Directory.EnumerateFiles(result.BuiltApplicationDirectory, "chunk_*-dxh*.wasm", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static List<string> FileHashes(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/') + " " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Materializes and builds the small frontend once, in Debug, into a backend project folder;
    /// then once more with <see cref="PlatformFrontendOptions.KeepFunctionNames"/>, into another.
    /// </summary>
    public sealed class BuiltFrontendFixture : IAsyncLifetime
    {
        /// <summary>The temporary folder with frontend/ and backend/.</summary>
        public TempFolder Folder { get; } = new TempFolder();

        /// <summary>Why the build cannot run on this machine, or null.</summary>
        public string SkipReason { get; private set; }

        /// <summary>The build's failure, or null.</summary>
        public Exception Failure { get; private set; }

        /// <summary>What the build made.</summary>
        public FrontendBuildResult Result { get; private set; }

        /// <summary>What the second build, with the function names kept, made.</summary>
        public FrontendBuildResult NamesResult { get; private set; }

        public async ValueTask InitializeAsync()
        {
            if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
            {
                return;
            }

            var dx = AdoptedProgram(HostTools.DxName);
            var wasmBindgen = AdoptedProgram(HostTools.WasmBindgenName);
            if (dx == null || !File.Exists(dx) || !File.Exists(wasmBindgen))
            {
                SkipReason = NoDxReason();
                return;
            }

            Folder.Write("frontend/Cargo.toml",
                "[package]\nname = \"dx-build-test\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n"
                + "dioxus = { version = \"=0.7.10\", default-features = false, features = [\"minimal\", \"web\", \"router\", \"document\", \"mounted\", \"asset\"] }\n"
                + "web-sys = { version = \"0.3\", features = [\"Document\", \"Element\", \"HtmlElement\", \"Location\", \"Window\"] }\n");
            Folder.Write("frontend/Dioxus.toml", "[application]\nname = \"dx-build-test\"\n");
            Folder.Write("frontend/tailwind.css", "@import \"tailwindcss\";\n");
            Folder.Write("frontend/src/main.rs",
                "use dioxus::prelude::*;\n\nconst TAILWIND_CSS: Asset = asset!(\"/assets/generated/tailwind.css\");\n\n"
                + "fn main() {\n    dioxus::launch(App);\n}\n\n#[component]\nfn App() -> Element {\n"
                + "    rsx! {\n        document::Stylesheet { href: TAILWIND_CSS }\n        div { class: \"flex text-indigo-600\", \"Hello\" }\n    }\n}\n"
                + "\nfn unused_helper() -> u32 {\n    42\n}\n");
            Directory.CreateDirectory(Folder.Combine("backend"));
            Directory.CreateDirectory(Folder.Combine("backend-names"));
            var options = new PlatformFrontendOptions
            {
                FrontendDirectory = Folder.Combine("frontend"),
                BackendAssemblyPath = Frontends.SampleBackendAssembly,
                DxPath = dx,
                WasmBindgenPath = wasmBindgen,
            };
            try
            {
                var frontend = new PlatformFrontend(options);
                await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

                // A class that only a stylesheet written during the build can hold.
                File.WriteAllText(Folder.Combine("frontend/src/main.rs"), File.ReadAllText(Folder.Combine("frontend/src/main.rs")).Replace("text-indigo-600", "text-indigo-600 text-fuchsia-900", StringComparison.Ordinal));

                // A target/ without its .gitignore (as after `cargo clean` and a cargo command),
                // holding a class name only an unignored target/ could give the stylesheet.
                File.Delete(Folder.Combine("frontend/target/.gitignore"));
                Folder.Write("frontend/target/stray/notes.html", "<div class=\"text-lime-300\"></div>\n");
                Result = await frontend.BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend"), TestContext.Current.CancellationToken);
                options.KeepFunctionNames = true;
                NamesResult = await new PlatformFrontend(options).BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend-names"), TestContext.Current.CancellationToken);
            }
            catch (FrontendBuildException exception)
            {
                Failure = exception;
            }
        }

        public ValueTask DisposeAsync()
        {
            Folder.Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// The dx or wasm-bindgen to build with: in the folder <see cref="DxFolderVariable"/>
        /// names, else the one adopted for this machine (native_libraries/&lt;rid&gt;/).
        /// </summary>
        internal static string AdoptedProgram(string name)
        {
            var rid = HostTools.CurrentRuntimeIdentifier;
            var suffix = rid != null && rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            var folder = Environment.GetEnvironmentVariable(DxFolderVariable);
            if (!string.IsNullOrEmpty(folder))
            {
                return Path.Combine(folder, name + suffix);
            }

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "native_libraries")))
                {
                    return Path.Combine(directory.FullName, "native_libraries", rid ?? "none", name + suffix);
                }
            }

            return null;
        }

        /// <summary>Why no build can be made: no dx where one is looked for.</summary>
        internal static string NoDxReason()
        {
            var folder = Environment.GetEnvironmentVariable(DxFolderVariable);
            return string.IsNullOrEmpty(folder)
                ? $"No dx is adopted for {HostTools.CurrentRuntimeIdentifier} in native_libraries/."
                : $"{DxFolderVariable} names {folder}, which holds no dx and wasm-bindgen.";
        }
    }

    /// <summary>
    /// Materializes a small frontend with bundle splitting - three lazy_loader! pieces and two
    /// lazy components, which share code in two different sets - and builds it in Debug three
    /// times, each into a backend project folder of its own: with a shared chunk threshold of
    /// one byte, the same again, and with a shared chunk limit of one.
    /// </summary>
    public sealed class SplitFrontendFixture : IAsyncLifetime
    {
        /// <summary>The temporary folder with frontend/ and the backend folders.</summary>
        public TempFolder Folder { get; } = new TempFolder();

        /// <summary>Why the builds cannot run on this machine, or null.</summary>
        public string SkipReason { get; private set; }

        /// <summary>A build's failure, or null.</summary>
        public Exception Failure { get; private set; }

        /// <summary>The build with a threshold of one byte.</summary>
        public FrontendBuildResult Low { get; private set; }

        /// <summary>The same build again.</summary>
        public FrontendBuildResult LowAgain { get; private set; }

        /// <summary>The build with a shared chunk limit of one.</summary>
        public FrontendBuildResult OneChunk { get; private set; }

        /// <summary>
        /// Files an earlier build could have left, put into dx's output folder and the built
        /// frontend folder before <see cref="OneChunk"/> is built.
        /// </summary>
        public static readonly string[] StaleFiles =
        {
            "chunk_7_split-dxh0123456789abcdef.wasm",
            "module_9_stale-dxh0123456789abcdef.wasm",
            "__wasm_split-dxh0123456789abcdef.js",
        };

        public async ValueTask InitializeAsync()
        {
            if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
            {
                return;
            }

            var dx = BuiltFrontendFixture.AdoptedProgram(HostTools.DxName);
            var wasmBindgen = BuiltFrontendFixture.AdoptedProgram(HostTools.WasmBindgenName);
            if (dx == null || !File.Exists(dx) || !File.Exists(wasmBindgen))
            {
                SkipReason = BuiltFrontendFixture.NoDxReason();
                return;
            }

            Folder.Write("frontend/Cargo.toml",
                "[package]\nname = \"dx-split-test\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n"
                + "dioxus = { version = \"=0.7.10\", default-features = false, features = [\"minimal\", \"web\", \"document\", \"asset\", \"wasm-split\"] }\n"
                + "web-sys = { version = \"0.3\", features = [\"Document\", \"Element\", \"HtmlElement\", \"Location\", \"Window\"] }\n");
            Folder.Write("frontend/Dioxus.toml", "[application]\nname = \"dx-split-test\"\n");
            Folder.Write("frontend/tailwind.css", "@import \"tailwindcss\";\n");
            Folder.Write("frontend/src/main.rs", SplitSource);
            foreach (var backend in new[] { "backend-low", "backend-low-again", "backend-one" })
            {
                Directory.CreateDirectory(Folder.Combine(backend));
            }

            var options = new PlatformFrontendOptions
            {
                FrontendDirectory = Folder.Combine("frontend"),
                BackendAssemblyPath = Frontends.SampleBackendAssembly,
                DxPath = dx,
                WasmBindgenPath = wasmBindgen,
                WasmSplit = true,
                SharedChunkThreshold = 1,
            };
            try
            {
                var frontend = new PlatformFrontend(options);
                await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
                Low = await frontend.BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend-low"), TestContext.Current.CancellationToken);
                LowAgain = await frontend.BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend-low-again"), TestContext.Current.CancellationToken);
                // What an earlier build could leave behind: files of other names in dx's output
                // folder and in the backend's built frontend folder. The build with fewer chunks
                // that follows must ship none of them (nor the earlier builds' chunks).
                foreach (var stale in StaleFiles)
                {
                    Folder.Write(Path.GetRelativePath(Folder.Path, Path.Combine(LowAgain.OutputDirectory, "assets", stale)), "stale");
                    Folder.Write("backend-one/" + BuiltFrontend.FolderName + "/assets/" + stale, "stale");
                }

                options.SharedChunkLimit = 1;
                OneChunk = await new PlatformFrontend(options).BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend-one"), TestContext.Current.CancellationToken);
            }
            catch (FrontendBuildException exception)
            {
                Failure = exception;
            }
        }

        public ValueTask DisposeAsync()
        {
            Folder.Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// The split frontend: pieces "first", "second" and "third" (lazy_loader!) and the lazy
        /// components Alpha and Beta (both in the piece "lazy"); first, second and Alpha share
        /// one function, second, third and Beta another.
        /// </summary>
        private const string SplitSource = """
            use dioxus::prelude::*;

            static FIRST: wasm_split::LazyLoader<u32, String> = wasm_split::lazy_loader!(extern "first" fn first_piece(n: u32) -> String);
            static SECOND: wasm_split::LazyLoader<u32, String> = wasm_split::lazy_loader!(extern "second" fn second_piece(n: u32) -> String);
            static THIRD: wasm_split::LazyLoader<u32, String> = wasm_split::lazy_loader!(extern "third" fn third_piece(n: u32) -> String);

            fn main() {
                dioxus::launch(App);
            }

            #[component]
            fn App() -> Element {
                let text = use_resource(|| async move {
                    let mut out = String::new();
                    if FIRST.load().await {
                        out.push_str(&FIRST.call(1).unwrap_or_default());
                    }
                    if SECOND.load().await {
                        out.push_str(&SECOND.call(2).unwrap_or_default());
                    }
                    if THIRD.load().await {
                        out.push_str(&THIRD.call(3).unwrap_or_default());
                    }
                    out
                });
                let shown = text.read().clone().unwrap_or_default();
                rsx! {
                    div { "{shown}" }
                    SuspenseBoundary {
                        fallback: |_| rsx! { "loading" },
                        Alpha {}
                        Beta {}
                    }
                }
            }

            fn first_piece(n: u32) -> String {
                format!("{}{}", shared_one_two(n), n)
            }

            fn second_piece(n: u32) -> String {
                format!("{}{}", shared_one_two(n), shared_two_three(n))
            }

            fn third_piece(n: u32) -> String {
                format!("{}{}", shared_two_three(n), n)
            }

            #[inline(never)]
            fn shared_one_two(n: u32) -> String {
                let letters: String = (0..n * 7).map(|i| char::from(b'a' + (i % 26) as u8)).collect();
                letters.repeat(3) + "one-two"
            }

            #[inline(never)]
            fn shared_two_three(n: u32) -> String {
                let mut values: Vec<u64> = (0..u64::from(n) * 11).collect();
                values.sort_by(|a, b| b.cmp(a));
                values.iter().map(|v| v.to_string()).collect::<Vec<_>>().join("-") + "two-three"
            }

            #[component(lazy)]
            fn Alpha() -> Element {
                rsx! { p { "{shared_one_two(4)}" } }
            }

            #[component(lazy)]
            fn Beta() -> Element {
                rsx! { p { "{shared_two_three(5)}" } }
            }
            """;
    }
}
