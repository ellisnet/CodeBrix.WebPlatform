using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>
/// How a <c>[FromQuery, WireRequired] string</c> parameter binds (declared as the platform's
/// samples declare one: nullable annotations off): MVC turns an empty or blank value into null
/// (ConvertEmptyStringToNull), so <c>name=</c> reaches the action exactly as a missing value does.
/// </summary>
public class QueryBindingTests : IClassFixture<PlumbingFixture>
{
    private readonly PlumbingFixture _fixture;

    public QueryBindingTests(PlumbingFixture fixture) => _fixture = fixture;

    public static readonly TheoryData<string> NullQueries = new() { "", "?name=", "?name", "?name=%20", "?other=x" };

    [Theory]
    [MemberData(nameof(NullQueries))]
    public async Task an_empty_blank_or_missing_required_query_string_binds_as_null(string query)
    {
        //Arrange
        using var client = _fixture.Host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/query" + query);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.TextAsync()).RootElement;
        body.GetProperty("isNull").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task a_non_empty_required_query_string_binds_as_its_value()
    {
        //Arrange
        using var client = _fixture.Host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/query?name=x");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.TextAsync()).RootElement;
        body.GetProperty("isNull").GetBoolean().Should().BeFalse();
        body.GetProperty("value").GetString().Should().Be("x");
    }
}
