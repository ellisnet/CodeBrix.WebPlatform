using System.Linq;
using CodeBrix.WebPlatform.BackendMapper.Reading;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests.Reading;

public class RouteTemplateTests
{
    [Theory]
    [InlineData("api/clients", "{id:guid}", "api/clients/{id:guid}")]
    [InlineData("api/clients", null, "api/clients")]
    [InlineData("api/clients/", "/status", "status")]
    [InlineData("api/clients", "~/status", "status")]
    [InlineData("", "ping", "ping")]
    [InlineData(null, null, "")]
    public void Combine_joins_the_two_templates_like_attribute_routing(string controller, string action, string expected)
        => RouteTemplate.Combine(controller, action).Should().Be(expected);

    [Fact]
    public void ReplaceTokens_fills_in_the_controller_and_action_names()
        => RouteTemplate.ReplaceTokens("api/[controller]/[action]", "Clients", "List").Should().Be("api/Clients/List");

    [Fact]
    public void Parse_splits_text_and_parameters_and_drops_constraints()
    {
        //Act
        var segments = RouteTemplate.Parse("api/clients/{id:guid}/notes/{noteId}", out var clean, out var unsupported);

        //Assert
        unsupported.Should().BeNull();
        clean.Should().Be("/api/clients/{id}/notes/{noteId}");
        segments.Select(s => s.Text).Should().Equal("/api/clients/", "id", "/notes/", "noteId");
    }

    [Fact]
    public void Parse_gives_a_route_without_parameters_as_one_piece()
    {
        //Act
        var segments = RouteTemplate.Parse("api/reports/export.csv", out var clean, out _);

        //Assert
        clean.Should().Be("/api/reports/export.csv");
        segments.Single().IsParameter.Should().BeFalse();
    }

    [Theory]
    [InlineData("api/{*rest}", "catch-all")]
    [InlineData("api/{**rest}", "catch-all")]
    [InlineData("api/{id?}", "optional")]
    [InlineData("api/{page=1}", "default value")]
    [InlineData("api/{id", "never closed")]
    [InlineData("api/{}", "no name")]
    public void Parse_refuses_what_the_mapper_does_not_support(string template, string reason)
    {
        //Act
        var segments = RouteTemplate.Parse(template, out _, out var unsupported);

        //Assert
        segments.Should().BeNull();
        unsupported.Should().Contain(reason);
    }
}
