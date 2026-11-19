using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Security;

public class AuthenticationRequiredTests : IClassFixture<ApiFactory>
{
    private const string AnyId = "01234567-89ab-cdef-0123-456789abcdef";

    private readonly ApiFactory _factory;

    public AuthenticationRequiredTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("PUT", "/api/auth/profile")]
    [InlineData("POST", "/api/auth/change-password")]
    [InlineData("GET", "/api/clients")]
    [InlineData("POST", "/api/clients")]
    [InlineData("GET", "/api/clients/" + AnyId)]
    [InlineData("PUT", "/api/clients/" + AnyId)]
    [InlineData("DELETE", "/api/clients/" + AnyId)]
    [InlineData("GET", "/api/projects")]
    [InlineData("POST", "/api/projects")]
    [InlineData("GET", "/api/projects/" + AnyId)]
    [InlineData("PUT", "/api/projects/" + AnyId)]
    [InlineData("DELETE", "/api/projects/" + AnyId)]
    [InlineData("GET", "/api/time-entries")]
    [InlineData("POST", "/api/time-entries")]
    [InlineData("GET", "/api/time-entries/" + AnyId)]
    [InlineData("PUT", "/api/time-entries/" + AnyId)]
    [InlineData("DELETE", "/api/time-entries/" + AnyId)]
    [InlineData("GET", "/api/dashboard")]
    [InlineData("GET", "/api/reports/summary")]
    [InlineData("GET", "/api/reports/export.csv")]
    public async Task authenticated_routes_answer_an_empty_401_when_signed_out(string method, string path)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        //Act
        var response = await client.SendAsync(request, withRequestedWith: true);

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task a_non_json_body_answers_415_with_the_contract_body()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostRawAsync("/api/clients", "name=x", "application/x-www-form-urlencoded");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/problem+json");
        (await ApiClient.JsonAsync(response)).GetProperty("status").GetInt32().Should().Be(415);
    }

    [Fact]
    public async Task malformed_json_answers_400_validation_failed()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostRawAsync("/api/clients", "{ \"name\": ", "application/json");

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().NotBeEmpty();
    }
}
