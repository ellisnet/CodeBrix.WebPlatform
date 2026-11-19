using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class HubOriginTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HubOriginTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage Negotiate(string hubPath, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, hubPath + "/negotiate?negotiateVersion=1");
        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
        }

        return request;
    }

    [Theory]
    [InlineData("/api/hubs/clock")]
    [InlineData("/api/hubs/activity")]
    [InlineData("/api/hubs/estimator")]
    public async Task a_public_hub_accepts_a_connection_from_the_sites_own_pages(string hubPath)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.SendAsync(Negotiate(hubPath, "https://localhost"), withRequestedWith: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiClient.JsonAsync(response)).GetProperty("connectionToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("/api/hubs/clock", "https://evil.example")]
    [InlineData("/api/hubs/clock", "http://localhost")]
    [InlineData("/api/hubs/clock", "https://localhost:444")]
    [InlineData("/api/hubs/clock", null)]
    [InlineData("/api/hubs/timer", "https://evil.example")]
    [InlineData("/api/hubs/timer", null)]
    [InlineData("/api/hubs/session", "null")]
    public async Task every_hub_refuses_a_connection_that_does_not_come_from_the_site(string hubPath, string origin)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.SendAsync(Negotiate(hubPath, origin), withRequestedWith: true);

        //Assert
        await ProblemAssertions.ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "forbidden_origin");
    }

    [Fact]
    public async Task the_websocket_handshake_is_refused_from_another_site_too()
    {
        //Act
        Func<Task> act = async () =>
        {
            await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/clock", origin: "https://evil.example");
        };

        //Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task the_negotiate_call_also_needs_the_requested_with_header()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.SendAsync(Negotiate("/api/hubs/clock", "https://localhost"));

        //Assert
        await ProblemAssertions.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Fact]
    public async Task requests_outside_the_hub_paths_are_not_affected()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/meta");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("https", "worksite.example", "https://worksite.example", true)]
    [InlineData("https", "worksite.example", "HTTPS://WorkSite.Example", true)]
    [InlineData("https", "worksite.example:8443", "https://worksite.example:8443", true)]
    [InlineData("https", "worksite.example", "http://worksite.example", false)]
    [InlineData("https", "worksite.example", "https://worksite.example.evil.test", false)]
    [InlineData("https", "worksite.example", "https://worksite.example/", false)]
    [InlineData("https", "worksite.example", "", false)]
    public void IsOwnOrigin_compares_the_scheme_and_host_exactly(string scheme, string host, string origin, bool expected)
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Headers.Origin = origin;

        //Act
        var result = HubOriginMiddleware.IsOwnOrigin(context.Request);

        //Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsOwnOrigin_refuses_a_request_without_the_header()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("worksite.example");

        //Assert
        HubOriginMiddleware.IsOwnOrigin(context.Request).Should().BeFalse();
    }
}
