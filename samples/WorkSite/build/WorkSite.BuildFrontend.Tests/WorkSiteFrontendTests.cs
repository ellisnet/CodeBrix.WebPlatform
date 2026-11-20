using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.BackendMapper;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.Core.Mapping;
using CodeBrix.WebPlatform.FrontendBuilder;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using WorkSite.BuildFrontend.Tests.Helpers;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class WorkSiteFrontendTests
{
    private static string BackendAssembly => Path.Combine(AppContext.BaseDirectory, "WorkSite.Api.dll");

    private static BackendMap MapBackend() => BackendMapper.Map(new BackendMapOptions { AssemblyPath = BackendAssembly, Types = WorkSiteFrontend.CreateTypeMap() });

    private static BuilderPaths ArrangePaths(TempFolder folder) => new BuilderPaths(new WorkSiteLayout(Path.Combine(folder.Path, "samples", "WorkSite")));

    [Fact]
    public void MapBackend_finds_the_real_backends_contract_fully_declared()
    {
        //Act
        var map = MapBackend();

        //Assert
        map.AssemblyName.Should().Be("WorkSite.Api");
        map.Controllers.Select(c => c.Name).Should().Equal("Auth", "Clients", "Dashboard", "Meta", "Projects", "Reports", "TimeEntries");
        map.Controllers.SelectMany(c => c.Endpoints).Select(e => e.RouteTemplate).Where(r => !r.StartsWith("/api/", StringComparison.Ordinal)).Should().BeEmpty();
        map.Structs.Select(s => s.Name).Should().Contain("TimeEntry");
        map.Structs.Select(s => s.Name).Should().Contain("UserResponse");
    }

    [Fact]
    public void MapBackend_finds_the_nine_hubs_under_the_api_path()
    {
        //Act
        var hubs = MapBackend().Hubs;

        //Assert
        hubs.Select(h => h.Name).Should().Equal("ActivityHub", "ChangesHub", "ClockHub", "EstimatorHub", "NotificationsHub",
            "ProjectFeedHub", "ReportProgressHub", "SessionHub", "TimerHub");
        hubs.Where(h => !h.Path.StartsWith("/api/hubs/", StringComparison.Ordinal)).Should().BeEmpty();
        hubs.Where(h => !h.RequiresSignIn).Select(h => h.Name).Should().Equal("ActivityHub", "ClockHub", "EstimatorHub");
    }

    [Fact]
    public void MapBackend_leaves_out_the_health_probe_and_the_no_such_route_controller()
    {
        //Act
        var controllers = MapBackend().Controllers.Select(c => c.Name).ToList();

        //Assert
        controllers.Should().NotContain("Health");
        controllers.Should().NotContain("ApiNotFound");
    }

    [Fact]
    public void the_export_is_a_file_address_and_the_refresh_call_is_open()
    {
        //Act
        var map = MapBackend();
        var export = map.Controllers.Single(c => c.Name == "Reports").Endpoints.Single(e => e.ActionName == "Export");
        var refresh = map.Controllers.Single(c => c.Name == "Auth").Endpoints.Single(e => e.ActionName == "Refresh");

        //Assert
        export.IsFile.Should().BeTrue();
        export.RouteTemplate.Should().Be("/api/reports/export.csv");
        refresh.RequiresSignIn.Should().BeFalse();
    }

    [Fact]
    public void the_backend_declares_its_request_marker_and_its_session_refresh_for_the_frontend()
    {
        //Act
        var map = MapBackend();

        //Assert
        map.RequestMarker.Should().Be(WireRequestMarkerAttribute.GetValue(typeof(WorkSite.Api.Program).Assembly));
        map.SessionRefresh.ActionName.Should().Be("Refresh");
        map.SessionRefresh.RouteTemplate.Should().Be("/api/auth/refresh");
        map.SessionRefreshController.Name.Should().Be("Auth");
        map.SessionRefresh.Response.Struct.Name.Should().Be("UserResponse");
    }

    [Fact]
    public void the_frontend_states_no_fact_about_the_backend_by_hand()
    {
        //Arrange
        var layout = WorkSiteLayout.Find(AppContext.BaseDirectory);
        var sources = Directory.EnumerateFiles(Path.Combine(layout.Frontend, "src"), "*.rs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(Path.Combine(layout.Frontend, "src", "generated"), StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(layout.Frontend, f), File.ReadAllText);

        //Assert
        sources.Should().NotBeEmpty();
        sources.Where(s => s.Value.Contains("X-Requested-With", StringComparison.Ordinal)).Select(s => s.Key).Should().BeEmpty("the generated configure_platform sends the declared marker");
        sources.Where(s => s.Value.Contains("/api/auth/refresh", StringComparison.Ordinal)).Select(s => s.Key).Should().BeEmpty("the session-refresh path is generated");
        sources.Where(s => s.Value.Contains("api::client::configure", StringComparison.Ordinal) || s.Value.Contains("signalr::configure", StringComparison.Ordinal)).Select(s => s.Key).Should().BeEmpty("the clients are set up by one generated call");
    }

    [Fact]
    public void CreateOptions_writes_the_client_code_with_the_platforms_default_options()
    {
        //Arrange
        using var folder = new TempFolder();
        var defaults = new RustClientOptions();

        //Act
        var options = WorkSiteFrontend.CreateOptions(ArrangePaths(folder), "4.3.5", null);

        //Assert
        options.ClientOptions.SignalRModulePath.Should().Be("codebrix_webplatform::signalr");
        options.ClientOptions.SignalRModulePath.Should().Be(defaults.SignalRModulePath);
        options.ClientOptions.SharedCratePath.Should().Be(defaults.SharedCratePath);
    }

    [Fact]
    public void CreateTypeMap_is_the_platforms_default_mapping()
    {
        //Act
        var found = WorkSiteFrontend.CreateTypeMap().TryGet("System.DateOnly", out var date);

        //Assert
        found.Should().BeTrue();
        date.Name.Should().Be("Date");
    }

    [Fact]
    public void CreateOptions_points_the_platform_at_the_WorkSite_folders_and_leaves_the_tools_to_the_package()
    {
        //Arrange
        using var folder = new TempFolder();
        var paths = ArrangePaths(folder);

        //Act
        var options = WorkSiteFrontend.CreateOptions(paths, "4.3.5", null);

        //Assert
        options.FrontendDirectory.Should().Be(paths.Layout.Frontend);
        options.BackendProjectPath.Should().Be(paths.ApiProject);
        options.BackendAssemblyPath.Should().Be(paths.ApiAssembly);
        options.DxPath.Should().BeNull("dx comes from the host-tools package");
        options.WasmBindgenPath.Should().BeNull();
        options.CrateMode.Should().Be(CrateMode.PlatformCratesOnly);
        options.ResolvedVendorDirectory.Should().Be(Path.Combine(paths.Layout.Frontend, "platform", "vendor"));
        options.DxHomeDirectory.Should().Be(paths.DxHome);
        paths.DxHome.Should().StartWith(paths.Artifacts);
        options.MinimumTailwindVersion.Should().Be("4.3.5");
        options.ServeFeatures.Should().Equal("devtools");
        options.SharedCrateDirectory.Should().Be(Path.Combine(paths.Layout.Frontend, "platform", "codebrix-webplatform"));
        options.GeneratedClientDirectory.Should().Be(Path.Combine(paths.Layout.Frontend, "src", "generated"));
    }

    [Fact]
    public async Task the_build_program_finds_dx_and_wasm_bindgen_in_the_host_tools_package()
    {
        //Arrange
        using var folder = new TempFolder();
        var frontend = new PlatformFrontend(WorkSiteFrontend.CreateOptions(ArrangePaths(folder), "4.3.5", null));

        //Act
        var report = await frontend.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        //Assert
        report[PrerequisiteKind.HostTools].Status.Should().Be(PrerequisiteStatus.Ok, report[PrerequisiteKind.HostTools].Message);
        report.HostTools.Source.Should().Be(HostToolsSource.Package);
        report[PrerequisiteKind.HostTools].Message.Should().Contain("package");
    }

    [Fact]
    public async Task materializing_from_the_built_backend_writes_the_four_files_the_shared_crate_and_the_crates()
    {
        //Arrange
        using var folder = new TempFolder();
        var paths = ArrangePaths(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ApiAssembly));
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            File.Copy(file, Path.Combine(Path.GetDirectoryName(paths.ApiAssembly), Path.GetFileName(file)));
        }

        folder.Write("samples/WorkSite/frontend/Cargo.toml", "[package]\nname = \"worksite-frontend\"\nversion = \"0.1.0\"\n");
        var options = WorkSiteFrontend.CreateOptions(paths, "4.3.5", null);
        options.BackendProjectPath = null;
        var frontend = new PlatformFrontend(options);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.GeneratedFiles.Should().HaveCount(4);
        second.WasUpToDate.Should().BeTrue();
        Directory.EnumerateFiles(options.GeneratedClientDirectory).Select(Path.GetFileName).Order().Should().Equal("api.rs", "hubs.rs", "mod.rs", "models.rs");
        File.ReadAllText(Path.Combine(options.GeneratedClientDirectory, "hubs.rs")).Should().Contain("use codebrix_webplatform::signalr::{self as signalr,");
        File.ReadAllText(Path.Combine(options.GeneratedClientDirectory, "mod.rs")).Should()
            .Contain("pub const REQUEST_MARKER: &str = \"WorkSite\";")
            .And.Contain("pub const SESSION_REFRESH_PATH: &str = \"/api/auth/refresh\";")
            .And.Contain("pub fn configure_platform(on_session_refreshed: impl Fn(UserResponse) + 'static, on_session_ended: impl Fn() + 'static)");
        File.Exists(Path.Combine(options.SharedCrateDirectory, "src", "lib.rs")).Should().BeTrue();
        File.Exists(Path.Combine(options.SharedCrateDirectory, "src", "signalr", "mod.rs")).Should().BeTrue();
        first.VendorChanged.Should().BeTrue();
        Directory.Exists(Path.Combine(options.ResolvedVendorDirectory, "dioxus-0.7.10")).Should().BeTrue();
        File.ReadAllText(options.CargoConfigurationFile).Should().Contain("directory = \"platform/vendor\"");
    }
}
