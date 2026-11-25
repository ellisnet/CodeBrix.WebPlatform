using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
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
/// with the reason. The build is made once for the class (<see cref="BuiltFrontendFixture"/>).
/// </summary>
public class PlatformFrontendDxBuildTests : IClassFixture<PlatformFrontendDxBuildTests.BuiltFrontendFixture>
{
    /// <summary>The variable that switches these tests on.</summary>
    public const string OptInVariable = "CODEBRIX_WEBPLATFORM_DX_BUILD_TESTS";

    private readonly BuiltFrontendFixture _built;

    public PlatformFrontendDxBuildTests(BuiltFrontendFixture built)
    {
        _built = built;
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

    /// <summary>Materializes and builds the small frontend once, in Debug, into a backend project folder.</summary>
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
                SkipReason = $"No dx is adopted for {HostTools.CurrentRuntimeIdentifier} in native_libraries/.";
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
                + "    rsx! {\n        document::Stylesheet { href: TAILWIND_CSS }\n        div { class: \"flex text-indigo-600\", \"Hello\" }\n    }\n}\n");
            Directory.CreateDirectory(Folder.Combine("backend"));
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
                Result = await frontend.BuildAsync(FrontendConfiguration.Debug, Folder.Combine("backend"), TestContext.Current.CancellationToken);
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

        private static string AdoptedProgram(string name)
        {
            var rid = HostTools.CurrentRuntimeIdentifier;
            var suffix = rid != null && rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "native_libraries")))
                {
                    return Path.Combine(directory.FullName, "native_libraries", rid ?? "none", name + suffix);
                }
            }

            return null;
        }
    }
}
