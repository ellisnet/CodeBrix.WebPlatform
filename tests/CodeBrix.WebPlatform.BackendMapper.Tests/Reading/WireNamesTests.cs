using CodeBrix.WebPlatform.BackendMapper.Reading;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests.Reading;

public class WireNamesTests
{
    [Theory]
    [InlineData("IsBillable", "isBillable")]
    [InlineData("ID", "id")]
    [InlineData("URLValue", "urlValue")]
    [InlineData("Name", "name")]
    [InlineData("already", "already")]
    [InlineData("X", "x")]
    [InlineData("", "")]
    public void JsonCamelCase_matches_the_serializers_web_defaults(string name, string expected)
        => WireNames.JsonCamelCase(name).Should().Be(expected);

    [Theory]
    [InlineData("IsBillable", "is_billable")]
    [InlineData("HTMLParser", "html_parser")]
    [InlineData("URLValue", "url_value")]
    [InlineData("partNumber", "part_number")]
    [InlineData("Export2Csv", "export2_csv")]
    [InlineData("ID", "id")]
    [InlineData("hours_today", "hours_today")]
    [InlineData("2Fast", "_2_fast")]
    public void SnakeCase_splits_words_the_way_rust_names_are_written(string name, string expected)
        => WireNames.SnakeCase(name).Should().Be(expected);

    [Theory]
    [InlineData("type", "r#type")]
    [InlineData("match", "r#match")]
    [InlineData("self", "self_")]
    [InlineData("name", "name")]
    public void RustIdentifier_escapes_keywords(string name, string expected)
        => WireNames.RustIdentifier(name).Should().Be(expected);

    [Theory]
    [InlineData("is_billable", "isBillable")]
    [InlineData("html_url", "htmlUrl")]
    [InlineData("name", "name")]
    public void SerdeCamelCase_predicts_what_serde_will_call_a_field(string name, string expected)
        => WireNames.SerdeCamelCase(name).Should().Be(expected);

    [Fact]
    public void ScreamingSnakeCase_names_a_constant()
        => WireNames.ScreamingSnakeCase("ClockHub").Should().Be("CLOCK_HUB");

    [Fact]
    public void TypeName_drops_the_generic_arity()
        => WireNames.TypeName("Page`1").Should().Be("Page");

    [Fact]
    public void RustString_escapes_quotes_and_backslashes()
        => WireNames.RustString("a\"b\\c").Should().Be("a\\\"b\\\\c");
}
