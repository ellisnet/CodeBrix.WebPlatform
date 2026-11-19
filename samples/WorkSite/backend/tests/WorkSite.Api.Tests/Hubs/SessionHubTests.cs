using System;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class SessionHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SessionHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task signing_out_tells_the_other_pages_of_the_same_session()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var otherTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/session", client);

        //Act
        (await client.PostAsync("/api/auth/logout")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var ended = await otherTab.NextAsync("Ended", TimeSpan.FromSeconds(5));

        //Assert
        ended[0].GetProperty("reason").GetString().Should().Be("SignedOut");
    }

    [Fact]
    public async Task signing_out_of_one_session_leaves_another_session_alone()
    {
        //Arrange
        using var laptop = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(laptop);
        using var phone = _factory.CreateApiClient();
        (await phone.PostAsync("/api/auth/login", new { identifier = user.GetProperty("username").GetString(), password = TestUsers.Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await using var phoneTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/session", phone);

        //Act
        await laptop.PostAsync("/api/auth/logout");

        //Assert
        (await phoneTab.StaysSilentAsync("Ended", TimeSpan.FromSeconds(1.5))).Should().BeTrue();
    }

    [Fact]
    public async Task changing_the_password_ends_the_other_sessions_and_keeps_this_one()
    {
        //Arrange
        using var laptop = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(laptop);
        using var phone = _factory.CreateApiClient();
        await phone.PostAsync("/api/auth/login", new { identifier = user.GetProperty("username").GetString(), password = TestUsers.Password });
        await using var laptopTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/session", laptop);
        await using var phoneTab = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/session", phone);

        //Act
        var response = await laptop.PostAsync("/api/auth/change-password", new { currentPassword = TestUsers.Password, newPassword = "a new long password" });
        var ended = await phoneTab.NextAsync("Ended", TimeSpan.FromSeconds(5));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ended[0].GetProperty("reason").GetString().Should().Be("PasswordChanged");
        (await laptopTab.StaysSilentAsync("Ended", TimeSpan.FromSeconds(1.5))).Should().BeTrue();
    }

    [Fact]
    public void OfSession_and_OfUserExcept_pick_the_right_connections()
    {
        //Arrange
        var connections = new SessionConnections();
        var user = Guid.NewGuid();
        var laptop = Guid.NewGuid();
        var phone = Guid.NewGuid();
        connections.Add("laptop-1", user, laptop);
        connections.Add("laptop-2", user, laptop);
        connections.Add("phone-1", user, phone);
        connections.Add("stranger", Guid.NewGuid(), Guid.NewGuid());

        //Act
        connections.Remove("laptop-2");

        //Assert
        connections.OfSession(laptop).Should().Equal("laptop-1");
        connections.OfUserExcept(user, laptop).Should().Equal("phone-1");
        connections.OfUserExcept(user, Guid.NewGuid()).Should().BeEquivalentTo(new[] { "laptop-1", "phone-1" });
    }

    [Fact]
    public void IdOf_writes_a_user_id_the_way_signalr_addresses_users()
    {
        //Arrange
        var userId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        //Assert
        HubUserIdProvider.IdOf(userId).Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
    }
}
