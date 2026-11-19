using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class HubAuthenticationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HubAuthenticationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage Negotiate(string hubPath)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, hubPath + "/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://localhost");
        return request;
    }

    [Theory]
    [InlineData("/api/hubs/timer")]
    [InlineData("/api/hubs/changes")]
    [InlineData("/api/hubs/notifications")]
    [InlineData("/api/hubs/report-progress")]
    [InlineData("/api/hubs/project-feed")]
    [InlineData("/api/hubs/session")]
    public async Task a_signed_in_hub_answers_an_empty_401_to_a_visitor(string hubPath)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.SendAsync(Negotiate(hubPath), withRequestedWith: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/hubs/timer")]
    [InlineData("/api/hubs/changes")]
    [InlineData("/api/hubs/notifications")]
    [InlineData("/api/hubs/report-progress")]
    [InlineData("/api/hubs/project-feed")]
    [InlineData("/api/hubs/session")]
    public async Task a_signed_in_hub_accepts_the_session_cookies(string hubPath)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.SendAsync(Negotiate(hubPath), withRequestedWith: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_signed_in_hub_refuses_the_websocket_handshake_of_a_visitor()
    {
        //Act
        Func<Task> act = async () =>
        {
            await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/timer");
        };

        //Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task a_signed_in_hub_stops_accepting_a_session_that_has_signed_out()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var cookies = client.CookieHeader("/api/hubs/timer");
        (await client.PostAsync("/api/auth/logout")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = Negotiate("/api/hubs/timer");
        request.Headers.Add("Cookie", cookies);
        using var anonymous = _factory.CreateApiClient();

        //Act
        var response = await anonymous.SendAsync(request, withRequestedWith: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
