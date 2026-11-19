using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Reading;
using CodeBrix.WebPlatform.BackendMapper.Tests.Helpers;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
using CodeBrix.WebPlatform.Core.Problems;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests;

public class BackendMapperTests
{
    private static RestController Widgets => Backends.Sample.Controllers.Single(c => c.Name == "Widgets");

    private static RestEndpoint Endpoint(string action) => Widgets.Endpoints.Single(e => e.ActionName == action);

    private static WireStruct Struct(string name) => Backends.Sample.Structs.Single(s => s.Name == name);

    private static WireField Field(string structName, string property) => Struct(structName).Fields.Single(f => f.ClrName == property);

    [Fact]
    public void Map_names_the_assembly_and_finds_the_controllers_in_name_order()
    {
        //Act
        var map = Backends.Sample;

        //Assert
        map.AssemblyName.Should().Be("CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend");
        map.Controllers.Select(c => c.Name).Should().Equal("Health", "Session", "Widgets");
        map.Controllers.Select(c => c.ClrName).Should().Contain("SampleBackend.Controllers.WidgetsController");
    }

    [Fact]
    public void Map_leaves_out_what_is_marked_WireIgnore_or_NonAction()
    {
        //Assert
        Backends.Sample.Controllers.Select(c => c.Name).Should().NotContain("Fallback");
        Widgets.Endpoints.Select(e => e.ActionName).Should().NotContain("Patch");
        Widgets.Endpoints.Select(e => e.ActionName).Should().NotContain("Helper");
        Struct("Widget").Fields.Select(f => f.ClrName).Should().NotContain("Internal");
    }

    [Fact]
    public void Map_joins_the_controller_and_action_routes_and_drops_constraints()
    {
        //Assert
        Endpoint("List").RouteTemplate.Should().Be("/api/Widgets");
        Endpoint("Get").RouteTemplate.Should().Be("/api/Widgets/{id}");
        Endpoint("ReplacePart").RouteTemplate.Should().Be("/api/Widgets/{id}/parts/{partNumber}");
        Endpoint("Status").RouteTemplate.Should().Be("/status");
        Endpoint("ReplacePart").Route.Select(s => s.Text).Should().Equal("/api/Widgets/", "id", "/parts/", "partNumber");
        Endpoint("ReplacePart").Route.Select(s => s.IsParameter).Should().Equal(false, true, false, true);
    }

    [Theory]
    [InlineData("List", "GET")]
    [InlineData("Create", "POST")]
    [InlineData("ReplacePart", "PUT")]
    [InlineData("DeleteAsync", "DELETE")]
    public void Map_reads_the_http_method(string action, string expected)
        => Endpoint(action).HttpMethod.Should().Be(expected);

    [Fact]
    public void Map_takes_who_may_call_from_the_action_or_else_the_controller()
    {
        //Assert
        Endpoint("List").RequiresSignIn.Should().BeTrue();
        Endpoint("Search").RequiresSignIn.Should().BeFalse();
        Backends.Sample.Controllers.Single(c => c.Name == "Health").Endpoints.Single().RequiresSignIn.Should().BeFalse();
    }

    [Fact]
    public void Map_orders_parameters_route_then_body_then_query_and_skips_services()
    {
        //Act
        var replace = Endpoint("ReplacePart").Parameters;
        var create = Endpoint("Create").Parameters;

        //Assert
        replace.Select(p => p.Name).Should().Equal("id", "partNumber", "part");
        replace.Select(p => p.Source).Should().Equal(ParameterSource.Route, ParameterSource.Route, ParameterSource.Body);
        create.Select(p => p.Name).Should().Equal("request", "notify");
        create.Select(p => p.Source).Should().Equal(ParameterSource.Body, ParameterSource.Query);
        Endpoint("Get").Parameters.Select(p => p.Name).Should().Equal("id");
        Endpoint("List").Parameters.Select(p => p.Name).Should().NotContain("cancellationToken");
    }

    [Fact]
    public void Map_decides_which_query_values_are_optional()
    {
        //Act
        var list = Endpoint("List").Parameters.ToDictionary(p => p.Name);

        //Assert
        list["from"].Type.IsOptional.Should().BeTrue();
        list["text"].Type.IsOptional.Should().BeTrue();
        list["shade"].Type.IsOptional.Should().BeFalse();
        list["page"].Type.IsOptional.Should().BeTrue();
        list["shade"].Type.Kind.Should().Be(WireTypeKind.Enum);
        Endpoint("Export").Parameters.Single(p => p.Name == "label").Type.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void Map_spreads_a_query_object_into_one_value_per_property()
    {
        //Act
        var search = Endpoint("Search").Parameters;

        //Assert
        search.Select(p => p.WireName).Should().Equal("text", "page", "shade");
        search.Select(p => p.Source).Distinct().Should().Equal(ParameterSource.Query);
        search.Select(p => p.Type.IsOptional).Should().Equal(true, false, true);
        Backends.Sample.Structs.Select(s => s.Name).Should().NotContain("WidgetSearch");
    }

    [Fact]
    public void Map_reads_the_success_answer_from_each_way_of_declaring_it()
    {
        //Assert
        Endpoint("List").Response.Struct.Name.Should().Be("PageOfWidget");
        Endpoint("Get").Response.Struct.Name.Should().Be("Widget");
        Endpoint("Create").Response.Struct.Name.Should().Be("Widget");
        Endpoint("Create").SuccessStatus.Should().Be(201);
        Endpoint("DeleteAsync").Response.Should().BeNull();
        Endpoint("DeleteAsync").SuccessStatus.Should().Be(204);
        Endpoint("Search").Response.Kind.Should().Be(WireTypeKind.List);
        Endpoint("Search").Response.Element.Struct.Name.Should().Be("Widget");
        Endpoint("Status").Response.Kind.Should().Be(WireTypeKind.Map);
    }

    [Fact]
    public void Map_marks_a_file_endpoint()
    {
        //Act
        var export = Endpoint("Export");

        //Assert
        export.IsFile.Should().BeTrue();
        export.Response.Should().BeNull();
        Endpoint("List").IsFile.Should().BeFalse();
    }

    [Fact]
    public void Map_reads_a_contract_class_field_by_field()
    {
        //Assert
        Field("Widget", "Id").Type.Rust.Name.Should().Be("String");
        Field("Widget", "Price").Type.Rust.Name.Should().Be("f64");
        Field("Widget", "Count").Type.Rust.Name.Should().Be("i32");
        Field("Widget", "MadeOn").Type.Rust.Name.Should().Be("Date");
        Field("Widget", "MadeOn").Type.Rust.Import.Should().Be(RustTypeMap.SharedDateImport);
        Field("Widget", "Tags").Type.Kind.Should().Be(WireTypeKind.List);
        Field("Widget", "Parts").Type.Element.Struct.Name.Should().Be("Part");
        Field("Widget", "Counts").Type.Kind.Should().Be(WireTypeKind.Map);
        Field("Widget", "Counts").Type.Element.Rust.Name.Should().Be("i32");
        Struct("Widget").ClrName.Should().Be("SampleBackend.Contracts.Widget");
        Struct("PageOfWidget").ClrName.Should().Be("SampleBackend.Contracts.Page<Widget>");
    }

    [Fact]
    public void Map_takes_optionality_from_nullable_value_types_and_the_nearest_wire_attribute()
    {
        //Assert
        Field("Widget", "Name").Type.IsOptional.Should().BeFalse();
        Field("Widget", "Notes").Type.IsOptional.Should().BeTrue();
        Field("Widget", "Stock").Type.IsOptional.Should().BeTrue();
        Field("Widget", "Count").Type.IsOptional.Should().BeFalse();
        Field("Widget", "RetiredOn").Type.IsOptional.Should().BeTrue();
        Field("Part", "Code").Type.IsOptional.Should().BeFalse();
        Field("WidgetRequest", "Name").Type.IsOptional.Should().BeTrue();
        Field("WidgetRequest", "Price").Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void Map_treats_a_nullable_value_marked_WireRequired_as_always_sent()
    {
        //Assert
        Field("WidgetRequest", "Count").Type.IsOptional.Should().BeFalse();
        Field("WidgetRequest", "Count").Type.Rust.Name.Should().Be("i32");
        Field("Widget", "Stock").Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void Map_follows_the_json_attributes()
    {
        //Assert
        Field("Widget", "HtmlUrl").JsonName.Should().Be("html_url");
        Field("Widget", "URLValue").JsonName.Should().Be("urlValue");
        Field("Widget", "MadeOn").JsonName.Should().Be("madeOn");
        Struct("Widget").Fields.Select(f => f.ClrName).Should().NotContain("Secret");
        Field("Widget", "Hint").Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void Map_applies_a_per_property_type_override()
    {
        //Act
        var rust = Field("Widget", "ExactPrice").Type.Rust;

        //Assert
        rust.Name.Should().Be("Money");
        rust.Import.Should().Be("crate::money::Money");
        rust.With.Should().Be("crate::money::as_text");
    }

    [Fact]
    public void Map_reads_enums_and_how_they_are_written()
    {
        //Act
        var shade = Backends.Sample.Enums.Single(e => e.Name == "Shade");
        var priority = Backends.Sample.Enums.Single(e => e.Name == "Priority");

        //Assert
        shade.AsText.Should().BeTrue();
        shade.Members.Select(m => m.Name).Should().Equal("Light", "Dark");
        priority.AsText.Should().BeFalse();
        priority.Members.Select(m => m.Value).Should().Equal(1L, 5L, 5L);
        priority.ClrName.Should().Be("SampleBackend.Contracts.Priority");
    }

    [Fact]
    public void Map_reads_a_hub_its_path_and_who_may_connect()
    {
        //Act
        var clock = Backends.Sample.Hubs.Single(h => h.Name == "ClockHub");
        var work = Backends.Sample.Hubs.Single(h => h.Name == "WorkHub");

        //Assert
        clock.Path.Should().Be("/hubs/clock");
        clock.RequiresSignIn.Should().BeFalse();
        clock.ClientInterfaceClrName.Should().Be("SampleBackend.Hubs.IClockClient");
        work.Path.Should().Be("/hubs/work");
        work.RequiresSignIn.Should().BeTrue();
    }

    [Fact]
    public void Map_reads_hub_methods_and_leaves_out_overrides()
    {
        //Act
        var clock = Backends.Sample.Hubs.Single(h => h.Name == "ClockHub");
        var work = Backends.Sample.Hubs.Single(h => h.Name == "WorkHub");

        //Assert
        clock.Methods.Select(m => m.Name).Should().Equal("Estimate", "Ping");
        clock.Methods[0].Kind.Should().Be(HubMethodKind.Invoke);
        clock.Methods[0].Arguments.Select(a => a.Name).Should().Equal("hours", "rate");
        clock.Methods[0].Result.Rust.Name.Should().Be("f64");
        clock.Methods[1].Result.Should().BeNull();
        work.Methods.Select(m => m.Name).Should().Equal("Generate", "JoinAsync", "Find");
        work.Methods[0].Kind.Should().Be(HubMethodKind.Stream);
        work.Methods[0].Result.Struct.Name.Should().Be("Progress");
        work.Methods[0].Arguments.Select(a => a.Name).Should().Equal("title", "steps");
        work.Methods[2].Result.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void Map_reads_the_messages_a_hub_sends_from_its_client_interface()
    {
        //Act
        var clock = Backends.Sample.Hubs.Single(h => h.Name == "ClockHub");

        //Assert
        clock.Messages.Select(m => m.Name).Should().Equal("Tick", "Reset", "Scores");
        clock.Messages[0].Arguments.Single().Type.Struct.Name.Should().Be("Tick");
        clock.Messages[1].Arguments.Should().BeEmpty();
        clock.Messages[2].Arguments.Select(a => a.Type.IsOptional).Should().Equal(false, false, true);
    }

    [Fact]
    public void Map_uses_an_application_wide_type_override()
    {
        //Arrange
        var options = new BackendMapOptions { AssemblyPath = Backends.SamplePath };
        options.Types.Map<decimal>(new RustType("Decimal") { Import = "crate::decimal::Decimal" });

        //Act
        var map = BackendMapper.Map(options);

        //Assert
        var price = map.Structs.Single(s => s.Name == "Widget").Fields.Single(f => f.ClrName == "Price").Type.Rust;
        price.Name.Should().Be("Decimal");
        price.Import.Should().Be("crate::decimal::Decimal");
    }

    [Fact]
    public void Map_reports_every_problem_of_a_backend_at_once()
    {
        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = Backends.BrokenPath });

        //Assert
        var problems = act.Should().Throw<BackendMapException>().Which.Problems.Select(p => p.ToString()).ToList();
        problems.Should().Contain(p => p.StartsWith("ThingsController:") && p.Contains("[ApiController]"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.NoResponse:") && p.Contains("success answer is not declared"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.NoAuth:") && p.Contains("[Authorize] or [AllowAnonymous]"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.NoRoute:") && p.Contains("attribute routing only"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.MissingRouteParameter:") && p.Contains("'{id}'"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.TwoShapes:") && p.Contains("one shape"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.UnmarkedBody(text):") && p.Contains("[WireRequired]"));
        problems.Should().Contain(p => p.StartsWith("Unmarked.Name:") && p.Contains("[WireOptional]"));
        problems.Should().Contain(p => p.StartsWith("Unmarked.Anything:") && p.Contains("no Rust type is mapped"));
        problems.Should().Contain(p => p.StartsWith("Unmarked.Self:") && p.Contains("contains itself directly"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.CatchAll:") && p.Contains("catch-all"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.WrongErrorBody(token):") && p.Contains("header or a form"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.WrongErrorBody:") && p.Contains("ApiProblem"));
        problems.Should().Contain(p => p.StartsWith("ThingsController.FileByPost:") && p.Contains("must answer GET"));
        problems.Should().Contain(p => p.StartsWith("UntypedHub:") && p.Contains("Hub<TClient>"));
        problems.Should().Contain(p => p.StartsWith("UntypedHub:") && p.Contains("[WireHub("));
        problems.Should().Contain(p => p.StartsWith("ReplyHub:") && p.Contains("who may connect"));
        problems.Should().Contain(p => p.StartsWith("IReplyClient.Ask:") && p.Contains("expects an answer"));
        problems.Should().Contain(p => p.StartsWith("ReplyHub.Send(numbers):") && p.Contains("stream from the frontend"));
        problems.Should().Contain(p => p.StartsWith("CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend:") && p.Contains("no action is marked [WireSessionRefresh]"));
        problems.Should().Contain(p => p.StartsWith("CodeBrix.WebPlatform.BackendMapper.Tests.BrokenBackend:") && p.Contains("[assembly: WireRequestMarker("));
        problems.Should().NotContain(p => p.StartsWith("RefreshMistakesController"));
    }

    [Fact]
    public void Map_reads_the_request_marker_and_the_session_refresh_action()
    {
        //Act
        var map = Backends.Sample;

        //Assert
        map.RequiresSignIn.Should().BeTrue();
        map.RequestMarker.Should().Be("SampleApp");
        map.SessionRefresh.RouteTemplate.Should().Be("/api/session/refresh");
        map.SessionRefresh.IsSessionRefresh.Should().BeTrue();
        map.SessionRefresh.Response.Struct.Name.Should().Be("Account");
        map.SessionRefreshController.Name.Should().Be("Session");
        map.Controllers.SelectMany(c => c.Endpoints).Count(e => e.IsSessionRefresh).Should().Be(1);
    }

    [Fact]
    public void Map_needs_no_session_refresh_or_marker_from_a_backend_without_sign_in()
    {
        //Act
        var map = BackendMapper.Map(new BackendMapOptions { AssemblyPath = typeof(ApiProblem).Assembly.Location });

        //Assert
        map.RequiresSignIn.Should().BeFalse();
        map.RequestMarker.Should().BeNull();
        map.SessionRefresh.Should().BeNull();
        map.SessionRefreshController.Should().BeNull();
    }

    [Fact]
    public void a_session_refresh_action_must_be_an_open_post_that_takes_nothing()
    {
        //Arrange
        using var assemblies = AssemblySet.Open(Backends.BrokenPath, Array.Empty<string>());
        var type = assemblies.Assembly.GetType("BrokenBackend.RefreshMistakesController");
        var context = new MapContext(new RustTypeMap());

        //Act
        var controller = new ControllerReader(context).Read(type);

        //Assert
        var problems = context.Problems.Select(p => p.ToString()).ToList();
        problems.Should().Contain(p => p.StartsWith("RefreshMistakesController.RefreshByGet:") && p.Contains("must answer POST"));
        problems.Should().Contain(p => p.StartsWith("RefreshMistakesController.RefreshByGet:") && p.Contains("[AllowAnonymous]"));
        problems.Should().Contain(p => p.StartsWith("RefreshMistakesController.RefreshByGet:") && p.Contains("must take nothing"));
        problems.Should().HaveCount(3);
        context.SessionRefreshLocations.Should().Equal("RefreshMistakesController.RefreshByGet", "RefreshMistakesController.Again");
        controller.Endpoints.Select(e => e.ActionName).Should().Equal("Again");
        controller.Endpoints[0].IsSessionRefresh.Should().BeTrue();
    }

    [Fact]
    public void Map_puts_the_count_and_each_problem_in_the_message()
    {
        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = Backends.BrokenPath });

        //Assert
        var exception = act.Should().Throw<BackendMapException>().Which;
        exception.Message.Should().StartWith($"The backend cannot be mapped: {exception.Problems.Count} problems.");
        exception.Message.Split('\n').Length.Should().Be(exception.Problems.Count + 1);
    }

    [Fact]
    public void Map_refuses_null_options()
    {
        //Act
        Action act = () => BackendMapper.Map(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Map_refuses_options_that_name_no_assembly(string path)
    {
        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = path });

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("*No backend assembly was named*");
    }

    [Fact]
    public void Map_says_so_when_the_assembly_does_not_exist()
    {
        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = Path.Combine(Path.GetTempPath(), "no-such-backend.dll") });

        //Assert
        var exception = act.Should().Throw<BackendMapException>().Which;
        exception.Message.Should().Contain("does not exist");
        exception.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Map_says_so_when_the_file_is_not_an_assembly()
    {
        //Arrange
        using var folder = new TempFolder();
        var file = folder.Write("NotAnAssembly.dll", "this is text");

        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = file });

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("*could not be read*");
    }

    [Fact]
    public void Map_refuses_a_reference_path_that_does_not_exist()
    {
        //Arrange
        var options = new BackendMapOptions { AssemblyPath = Backends.SamplePath };
        options.ReferencePaths.Add(Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N")));

        //Act
        Action act = () => BackendMapper.Map(options);

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("*reference path*does not exist*");
    }

    [Fact]
    public void Map_refuses_a_null_type_map()
    {
        //Act
        Action act = () => BackendMapper.Map(new BackendMapOptions { AssemblyPath = Backends.SamplePath, Types = null });

        //Assert
        act.Should().Throw<BackendMapException>().WithMessage("*Types is null*");
    }
}
