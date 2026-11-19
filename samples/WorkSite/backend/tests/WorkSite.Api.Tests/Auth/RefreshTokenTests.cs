using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Auth;

public class RefreshTokenTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RefreshTokenTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task refresh_rotates_both_cookies_and_returns_the_user()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);
        var oldRefresh = client.Cookie("ws_refresh");
        var oldAccess = client.Cookie("ws_access");

        //Act
        var response = await client.PostAsync("/api/auth/refresh");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiClient.JsonAsync(response)).GetProperty("id").GetString().Should().Be(user.GetProperty("id").GetString());
        client.Cookie("ws_refresh").Should().NotBe(oldRefresh);
        client.Cookie("ws_access").Should().NotBe(oldAccess);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task refresh_can_be_repeated_with_each_new_token()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var first = await client.PostAsync("/api/auth/refresh");
        var second = await client.PostAsync("/api/auth/refresh");
        var third = await client.PostAsync("/api/auth/refresh");

        //Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task refresh_without_a_refresh_cookie_answers_an_empty_401()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/refresh");

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task refresh_with_an_unknown_token_answers_an_empty_401()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        client.SetCookie("ws_refresh", "made-up-token", "/api/auth");

        //Act
        var response = await client.PostAsync("/api/auth/refresh");

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task reusing_a_replaced_refresh_token_revokes_the_whole_session()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var stolen = client.Cookie("ws_refresh");
        (await client.PostAsync("/api/auth/refresh")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var attacker = _factory.CreateApiClient();
        attacker.SetCookie("ws_refresh", stolen, "/api/auth");

        //Act
        var reuse = await attacker.PostAsync("/api/auth/refresh");

        //Assert
        await reuse.ShouldBeEmptyUnauthorizedAsync();
        await (await client.GetAsync("/api/auth/me")).ShouldBeEmptyUnauthorizedAsync();
        await (await client.PostAsync("/api/auth/refresh")).ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task the_refresh_cookie_is_never_sent_outside_the_auth_routes()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        client.SetCookie("ws_access", "", "/nowhere");

        //Act
        var response = await client.GetAsync("/api/clients");

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }
}
