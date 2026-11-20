using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Rust;
using CodeBrix.WebPlatform.BackendMapper.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests.Rust;

public class RustClientGeneratorTests
{
    private static string File(string name, RustClientOptions options = null)
        => RustClientGenerator.Generate(Backends.Sample, options).Single(f => f.RelativePath == name).Content;

    [Fact]
    public void Generate_writes_the_four_files_each_starting_with_the_marker()
    {
        //Act
        var files = RustClientGenerator.Generate(Backends.Sample);

        //Assert
        files.Select(f => f.RelativePath).Should().Equal("api.rs", "hubs.rs", "mod.rs", "models.rs");
        foreach (var file in files)
        {
            file.Content.Should().StartWith(RustClientGenerator.GeneratedMarker);
            file.Content.Should().EndWith("\n");
            file.Content.Should().NotContain("\r");
            file.Content.Should().NotContain("\n\n\n");
        }
    }

    [Fact]
    public void Generate_declares_the_modules()
    {
        //Act
        var module = File("mod.rs");

        //Assert
        module.Should().Contain("pub mod models;\npub mod api;\npub mod hubs;\n");
        module.Should().Contain("#![allow(dead_code, unused_imports)]");
    }

    [Fact]
    public void Generate_writes_a_struct_per_contract_class()
    {
        //Act
        var models = File("models.rs");

        //Assert
        models.Should().Contain("#[serde(rename_all = \"camelCase\")]\npub struct Widget {\n    pub id: String,\n    pub name: String,\n    #[serde(default)]\n    pub notes: Option<String>,\n    pub price: f64,");
        models.Should().Contain("    #[serde(with = \"crate::money::as_text\")]\n    pub exact_price: Money,");
        models.Should().Contain("    #[serde(rename = \"html_url\")]\n    pub html_url: String,");
        models.Should().Contain("    pub url_value: String,");
        models.Should().Contain("    pub r#type: String,");
        models.Should().Contain("    pub counts: BTreeMap<String, i32>,");
        models.Should().Contain("pub struct PageOfWidget {\n    pub items: Vec<Widget>,\n    pub total: i32,\n}");
        models.Should().NotContain("secret");
    }

    [Fact]
    public void Generate_imports_what_the_models_use()
    {
        //Act
        var models = File("models.rs");

        //Assert
        models.Should().Contain("use serde::{Deserialize, Serialize};");
        models.Should().Contain("use codebrix_webplatform::dates::Date;");
        models.Should().Contain("use crate::money::Money;");
        models.Should().Contain("use std::collections::BTreeMap;");
    }

    [Fact]
    public void Generate_derives_Default_only_where_every_field_has_one()
    {
        //Act
        var models = File("models.rs");

        //Assert
        models.Should().Contain("#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]\n#[serde(rename_all = \"camelCase\")]\npub struct Part {");
        models.Should().Contain("#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]\n#[serde(rename_all = \"camelCase\")]\npub struct Widget {");
    }

    [Fact]
    public void Generate_writes_a_text_enum_with_serde_and_a_number_enum_by_hand()
    {
        //Act
        var models = File("models.rs");

        //Assert
        models.Should().Contain("#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash, Serialize, Deserialize)]\npub enum Shade {\n    #[default]\n    Light,\n    Dark,\n}");
        models.Should().Contain("#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]\npub enum Priority {");
        models.Should().Contain("impl Serialize for Priority {");
        models.Should().Contain("pub const ALL: [Priority; 3] = [Priority::Low, Priority::High, Priority::Urgent];");
        models.Should().Contain("            1 => Some(Priority::Low),\n            5 => Some(Priority::High),\n            _ => None,");
        models.Should().NotContain("impl Serialize for Shade");
    }

    [Fact]
    public void Generate_writes_a_function_per_endpoint_in_a_module_per_controller()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("pub mod widgets {\n    use super::*;");
        api.Should().Contain("    pub async fn get(id: &str) -> ApiResult<Widget> {\n        client::send_json(Method::GET, &format!(\"/api/Widgets/{}\", encode(id)), None, Auth::Session).await\n    }");
        api.Should().Contain("    pub async fn replace_part(id: &str, part_number: i32, part: &Part) -> ApiResult<Part> {\n        client::send_json(Method::PUT, &format!(\"/api/Widgets/{}/parts/{}\", encode(id), encode(&part_number.to_string())), Some(client::body(part)?), Auth::Session).await");
        api.Should().Contain("    pub async fn delete(id: &str) -> ApiResult<()> {\n        client::send_empty(Method::DELETE,");
        api.Should().Contain("    pub async fn check() -> ApiResult<()> {\n        client::send_empty(Method::GET, \"/api/health\", None, Auth::Public).await");
        api.Should().Contain("    pub async fn status() -> ApiResult<BTreeMap<String, String>> {");
    }

    [Fact]
    public void Generate_takes_a_few_query_values_as_arguments_and_many_as_a_struct()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("    pub async fn create(request: &WidgetRequest, notify: bool) -> ApiResult<Widget> {\n        let query_string = QueryString::new().push(\"notify\", notify).build();");
        api.Should().Contain("    pub struct ListQuery {\n        pub from: Option<Date>,\n        pub to: Option<Date>,\n        pub text: Option<String>,\n        pub shade: Shade,\n        pub page: Option<i32>,\n    }");
        api.Should().Contain("    pub async fn list(query: &ListQuery) -> ApiResult<PageOfWidget> {\n        let query_string = QueryString::new().push_opt(\"from\", query.from).push_opt(\"to\", query.to).push_opt(\"text\", query.text.as_ref()).push(\"shade\", query.shade).push_opt(\"page\", query.page).build();");
    }

    [Fact]
    public void Generate_respects_the_query_argument_limit()
    {
        //Act
        var api = File("api.rs", new RustClientOptions { MaxSeparateQueryArguments = 10 });

        //Assert
        api.Should().NotContain("pub struct ListQuery");
        api.Should().Contain("pub async fn list(from: Option<Date>, to: Option<Date>, text: Option<&str>, shade: Shade, page: Option<i32>) -> ApiResult<PageOfWidget> {");
    }

    [Fact]
    public void Generate_writes_an_address_builder_for_a_file_endpoint()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("    pub fn export_url(from: Option<Date>, label: &str) -> String {\n        let query_string = QueryString::new().push_opt(\"from\", from).push(\"label\", label).build();\n        format!(\"/api/Widgets/export.csv{}\", query_string)\n    }");
    }

    [Fact]
    public void Generate_writes_a_bytes_reader_beside_the_address_builder_of_a_file_endpoint()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("        format!(\"/api/Widgets/export.csv{}\", query_string)\n    }\n\n    /// `GET /api/Widgets/export.csv` (needs sign-in), the file read as bytes.\n    pub async fn export_bytes(from: Option<Date>, label: &str) -> ApiResult<Vec<u8>> {\n        let query_string = QueryString::new().push_opt(\"from\", from).push(\"label\", label).build();\n        client::send_bytes(Method::GET, &format!(\"/api/Widgets/export.csv{}\", query_string), None, Auth::Session).await\n    }");
    }

    [Fact]
    public void Generate_passes_a_literal_path_to_the_bytes_reader_and_an_owned_one_to_the_address_builder()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("    pub fn manual_url() -> String {\n        \"/api/Widgets/manual.pdf\".to_string()\n    }");
        api.Should().Contain("    pub async fn manual_bytes() -> ApiResult<Vec<u8>> {\n        client::send_bytes(Method::GET, \"/api/Widgets/manual.pdf\", None, Auth::Session).await\n    }");
    }

    [Fact]
    public void Generate_gives_the_bytes_reader_the_route_values_and_sign_in_rule_of_its_action()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().Contain("    /// The address of `GET /api/Widgets/{id}/picture.png` (open to everyone), a file for the browser to fetch.\n    pub fn picture_url(id: &str) -> String {\n        format!(\"/api/Widgets/{}/picture.png\", encode(id))\n    }");
        api.Should().Contain("    /// `GET /api/Widgets/{id}/picture.png` (open to everyone), the file read as bytes.\n    pub async fn picture_bytes(id: &str) -> ApiResult<Vec<u8>> {\n        client::send_bytes(Method::GET, &format!(\"/api/Widgets/{}/picture.png\", encode(id)), None, Auth::Public).await\n    }");
    }

    [Fact]
    public void Generate_writes_no_bytes_reader_for_an_endpoint_that_answers_json()
    {
        //Act
        var api = File("api.rs");

        //Assert
        api.Should().NotContain("pub async fn get_bytes");
        api.Should().NotContain("pub async fn list_bytes");
        api.Should().NotContain("send_bytes(Method::POST");
    }

    [Fact]
    public void Generate_writes_a_descriptor_and_a_type_per_hub()
    {
        //Act
        var hubs = File("hubs.rs");

        //Assert
        hubs.Should().Contain("pub static CLOCK_HUB: HubDescriptor = HubDescriptor {\n    name: \"ClockHub\",\n    path: \"/hubs/clock\",\n    requires_sign_in: false,\n};");
        hubs.Should().Contain("    requires_sign_in: true,");
        hubs.Should().Contain("pub struct ClockHub {\n    connection: HubConnection,\n}");
        hubs.Should().Contain("        ClockHub { connection: signalr::connect(&CLOCK_HUB) }");
        hubs.Should().Contain("        ClockHub { connection: signalr::use_hub(&CLOCK_HUB) }");
    }

    [Fact]
    public void Generate_writes_hub_methods_as_invocations_or_streams()
    {
        //Act
        var hubs = File("hubs.rs");

        //Assert
        hubs.Should().Contain("    pub async fn estimate(&self, hours: f64, rate: f64) -> Result<f64, HubError> {\n        self.connection.invoke(\"Estimate\", vec![to_arg(&hours), to_arg(&rate)]).await");
        hubs.Should().Contain("    pub async fn ping(&self) -> Result<(), HubError> {\n        self.connection.invoke(\"Ping\", Vec::new()).await");
        hubs.Should().Contain("    pub fn generate(&self, title: &str, steps: i32) -> HubStream<Progress> {\n        self.connection.stream(\"Generate\", vec![to_arg(&title), to_arg(&steps)])");
        hubs.Should().Contain("    pub async fn join(&self, project: &str) -> Result<(), HubError> {\n        self.connection.invoke(\"JoinAsync\",");
        hubs.Should().Contain("    pub async fn find(&self, name: &str) -> Result<Option<Widget>, HubError> {");
    }

    [Fact]
    public void Generate_writes_a_handler_and_a_latest_value_hook_per_message()
    {
        //Act
        var hubs = File("hubs.rs");

        //Assert
        hubs.Should().Contain("fn decode_clock_hub_tick(args: &[Value]) -> Option<Tick> {\n    arg::<Tick>(args, 0)\n}");
        hubs.Should().Contain("    pub fn on_tick(&self, handler: impl Fn(Tick) + 'static) -> Subscription {\n        self.connection.on(\"Tick\", move |args| {");
        hubs.Should().Contain("    pub fn use_tick() -> ReadSignal<Option<Tick>> {\n        signalr::use_hub_message(&CLOCK_HUB, \"Tick\", decode_clock_hub_tick)");
        hubs.Should().Contain("fn decode_clock_hub_reset(_args: &[Value]) -> Option<()> {\n    Some(())\n}");
        hubs.Should().Contain("pub struct ClockHubScoresArgs {\n    pub team: String,\n    pub points: i32,\n    pub note: Option<String>,\n}");
        hubs.Should().Contain("    Some(ClockHubScoresArgs { team: arg::<String>(args, 0)?, points: arg::<i32>(args, 1)?, note: arg::<Option<String>>(args, 2)? })");
    }

    [Fact]
    public void Generate_uses_the_paths_the_application_names()
    {
        //Arrange
        var options = new RustClientOptions { SharedCratePath = "crate::platform", SignalRModulePath = "crate::signalr" };

        //Act
        var api = File("api.rs", options);
        var hubs = File("hubs.rs", options);
        var models = File("models.rs", options);

        //Assert
        api.Should().Contain("use crate::platform::api::client::{self, Auth, Method};");
        File("mod.rs", options).Should().Contain("use crate::platform::PlatformConfig;").And.Contain("crate::platform::configure(PlatformConfig {").And.Contain("crate::platform::setup::json_hook(");
        api.Should().Contain("use crate::platform::dates::Date;");
        models.Should().Contain("use crate::platform::dates::Date;");
        hubs.Should().Contain("use crate::signalr::{self as signalr, arg, to_arg,");
    }

    [Fact]
    public void Generate_writes_what_the_backend_declares_and_the_one_set_up_call()
    {
        //Act
        var module = File("mod.rs");

        //Assert
        module.Should().Contain("use std::rc::Rc;\n\nuse codebrix_webplatform::PlatformConfig;\n\nuse self::models::*;\n");
        module.Should().Contain("pub const REQUEST_MARKER: &str = \"SampleApp\";");
        module.Should().Contain("/// Where an expired session is renewed: `SampleBackend.Controllers.SessionController.Refresh` (`[WireSessionRefresh]`).\npub const SESSION_REFRESH_PATH: &str = \"/api/session/refresh\";");
        module.Should().Contain("pub fn configure_platform(on_session_refreshed: impl Fn(Account) + 'static, on_session_ended: impl Fn() + 'static) {\n"
            + "    codebrix_webplatform::configure(PlatformConfig {\n"
            + "        request_marker: Some(REQUEST_MARKER.to_string()),\n"
            + "        session_refresh_path: Some(SESSION_REFRESH_PATH.to_string()),\n"
            + "        on_session_refreshed: Some(codebrix_webplatform::setup::json_hook(on_session_refreshed)),\n"
            + "        on_session_ended: Some(Rc::new(on_session_ended)),\n"
            + "    });\n}\n");
        module.Should().Contain("handed the answer of a successful refresh (`Account`).");
    }

    [Fact]
    public void Generate_writes_a_set_up_call_without_session_hooks_for_a_backend_without_sign_in()
    {
        //Arrange
        var map = new BackendMap("PublicApp") { RequestMarker = "My \"App\"" };

        //Act
        var module = RustClientGenerator.Generate(map).Single(f => f.RelativePath == "mod.rs").Content;

        //Assert
        module.Should().Contain("pub const REQUEST_MARKER: &str = \"My \\\"App\\\"\";");
        module.Should().NotContain("SESSION_REFRESH_PATH");
        module.Should().NotContain("use self::models::*;");
        module.Should().Contain("pub fn configure_platform() {\n"
            + "    codebrix_webplatform::configure(PlatformConfig {\n"
            + "        request_marker: Some(REQUEST_MARKER.to_string()),\n"
            + "        ..PlatformConfig::default()\n"
            + "    });\n}\n");
    }

    [Fact]
    public void Generate_sends_no_marker_a_backend_does_not_declare()
    {
        //Act
        var module = RustClientGenerator.Generate(new BackendMap("PublicApp")).Single(f => f.RelativePath == "mod.rs").Content;

        //Assert
        module.Should().NotContain("REQUEST_MARKER");
        module.Should().Contain("        request_marker: None,\n");
    }

    [Fact]
    public void Generate_hands_a_refresh_answer_without_a_body_to_a_hook_without_arguments()
    {
        //Arrange
        var controller = new RestController("Auth", "App.Controllers.AuthController");
        var map = new BackendMap("App")
        {
            RequestMarker = "App",
            SessionRefresh = new RestEndpoint("Renew", "POST", "/api/auth/renew") { IsSessionRefresh = true, SuccessStatus = 204 },
            SessionRefreshController = controller,
        };

        //Act
        var module = RustClientGenerator.Generate(map).Single(f => f.RelativePath == "mod.rs").Content;

        //Assert
        module.Should().Contain("pub const SESSION_REFRESH_PATH: &str = \"/api/auth/renew\";");
        module.Should().Contain("pub fn configure_platform(on_session_refreshed: impl Fn() + 'static, on_session_ended: impl Fn() + 'static) {");
        module.Should().Contain("        on_session_refreshed: Some(Rc::new(move |_: &str| on_session_refreshed())),");
        module.Should().Contain("handed the answer of a successful refresh.");
    }

    [Fact]
    public void Generate_applies_a_replaced_template()
    {
        //Arrange
        var options = new RustClientOptions();
        options.Templates.Module = "{{ marker }}\n// hubs: {{ for h in hubs }}{{ h.name }} {{ end }}\n";

        //Act
        var module = File("mod.rs", options);

        //Assert
        module.Should().Be(RustClientGenerator.GeneratedMarker + "\n// hubs: ClockHub WorkHub\n");
    }

    [Fact]
    public void Generate_reports_a_template_with_a_syntax_error()
    {
        //Arrange
        var options = new RustClientOptions();
        options.Templates.Api = "{{ for c in controllers }} never closed";

        //Act
        Action act = () => RustClientGenerator.Generate(Backends.Sample, options);

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("The 'api' template has errors:*");
    }

    [Fact]
    public void Generate_reports_a_template_that_fails_while_applied()
    {
        //Arrange
        var options = new RustClientOptions();
        options.Templates.Hubs = "{{ no_such_value }}";

        //Act
        Action act = () => RustClientGenerator.Generate(Backends.Sample, options);

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("The 'hubs' template failed while it was applied:*");
    }

    [Fact]
    public void Generate_refuses_an_empty_template_and_a_null_map()
    {
        //Arrange
        var options = new RustClientOptions();
        options.Templates.Models = " ";
        Action empty = () => RustClientGenerator.Generate(Backends.Sample, options);
        Action missing = () => RustClientGenerator.Generate(null);

        //Assert
        empty.Should().Throw<BackendMapException>().WithMessage("*'models' template is empty*");
        missing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WriteTo_writes_the_files_and_then_leaves_an_up_to_date_folder_alone()
    {
        //Arrange
        using var folder = new TempFolder();
        var target = Path.Combine(folder.Path, "src", "generated");
        var files = RustClientGenerator.Generate(Backends.Sample);

        //Act
        var first = RustClientGenerator.WriteTo(target, files);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(Path.Combine(target, "api.rs"), stamp);
        var second = RustClientGenerator.WriteTo(target, files);

        //Assert
        first.Should().Equal("api.rs", "hubs.rs", "mod.rs", "models.rs");
        second.Should().BeEmpty();
        System.IO.File.GetLastWriteTimeUtc(Path.Combine(target, "api.rs")).Should().Be(stamp);
        System.IO.File.ReadAllText(Path.Combine(target, "models.rs")).Should().Be(files.Single(f => f.RelativePath == "models.rs").Content);
    }

    [Fact]
    public void WriteTo_rewrites_a_changed_file_and_removes_a_stale_generated_one()
    {
        //Arrange
        using var folder = new TempFolder();
        var files = RustClientGenerator.Generate(Backends.Sample);
        RustClientGenerator.WriteTo(folder.Path, files);
        folder.Write("api.rs", "// edited");
        folder.Write("old.rs", RustClientGenerator.GeneratedMarker + " from an earlier build\n");

        //Act
        var changed = RustClientGenerator.WriteTo(folder.Path, files);

        //Assert
        changed.Should().Equal("api.rs", "old.rs");
        System.IO.File.Exists(Path.Combine(folder.Path, "old.rs")).Should().BeFalse();
    }

    [Fact]
    public void WriteTo_refuses_a_folder_that_holds_hand_written_code()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("mine.rs", "// written by hand\n");

        //Act
        Action act = () => RustClientGenerator.WriteTo(folder.Path, RustClientGenerator.Generate(Backends.Sample));

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'mine.rs', which was not generated*");
        Directory.EnumerateFiles(folder.Path).Count().Should().Be(1);
    }

    [Fact]
    public void WriteTo_refuses_a_blank_folder_or_no_files()
    {
        //Arrange
        Action blank = () => RustClientGenerator.WriteTo(" ", Array.Empty<GeneratedFile>());
        Action missing = () => RustClientGenerator.WriteTo(Path.GetTempPath(), null);

        //Assert
        blank.Should().Throw<ArgumentException>();
        missing.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("mod")]
    [InlineData("models")]
    [InlineData("api")]
    [InlineData("hubs")]
    public void BuiltIn_returns_each_built_in_template(string name)
        => RustTemplates.BuiltIn(name).Should().Contain("{{ marker }}");

    [Fact]
    public void BuiltIn_refuses_an_unknown_name()
    {
        //Act
        Action act = () => RustTemplates.BuiltIn("nope");

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*no built-in template named 'nope'*");
    }
}
