using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

public class HubOriginTests : IClassFixture<PlumbingFixture>
{
    private const string Negotiate = "/api/hubs/echo/negotiate?negotiateVersion=1";

    private readonly BackendHost _host;

    public HubOriginTests(PlumbingFixture fixture)
    {
        _host = fixture.Host;
    }

    [Fact]
    public async Task a_hub_accepts_the_negotiate_call_from_the_sites_own_pages()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, withMarker: true, origin: BackendHost.OwnOrigin);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("connectionToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost")]
    [InlineData("https://localhost:444")]
    [InlineData("https://localhost/")]
    [InlineData("null")]
    [InlineData(null)]
    public async Task a_hub_refuses_a_request_that_does_not_come_from_the_site(string origin)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, withMarker: true, origin: origin);

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden_origin");
        problem.GetProperty("title").GetString().Should().Be("The connection was refused because it did not come from this site.");
    }

    [Theory]
    [InlineData("/api/hubs")]
    [InlineData("/api/hubs/no-such-hub")]
    [InlineData("/API/HUBS/echo")]
    public async Task every_address_below_the_hub_prefix_is_covered(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path, origin: "https://evil.example");

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden_origin");
    }

    [Fact]
    public async Task the_negotiate_call_needs_the_request_marker_too()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, origin: BackendHost.OwnOrigin);

        //Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Fact]
    public async Task requests_outside_the_hub_addresses_need_no_origin()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", "/api/echo", origin: "https://evil.example");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task the_websocket_handshake_from_another_site_is_refused()
    {
        //Arrange
        var socket = _host.Server.CreateWebSocketClient();
        socket.ConfigureRequest = request => request.Headers.Origin = "https://evil.example";

        //Act
        Func<Task> act = () => socket.ConnectAsync(new Uri("wss://localhost/api/hubs/echo"), TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task a_websocket_from_the_sites_own_pages_reaches_the_hub()
    {
        //Arrange
        using var client = _host.CreateClient();
        var negotiate = await client.SendAsync("POST", Negotiate, withMarker: true, origin: BackendHost.OwnOrigin);
        var token = JsonDocument.Parse(await negotiate.TextAsync()).RootElement.GetProperty("connectionToken").GetString();
        var socketClient = _host.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request => request.Headers.Origin = BackendHost.OwnOrigin;
        var cancellation = TestContext.Current.CancellationToken;

        //Act
        using var socket = await socketClient.ConnectAsync(new Uri("wss://localhost/api/hubs/echo?id=" + token), cancellation);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, cancellation);
        var buffer = new byte[256];
        var received = await socket.ReceiveAsync(buffer, cancellation);

        //Assert
        Encoding.UTF8.GetString(buffer, 0, received.Count).Should().Be("{}\u001e");
    }
}
