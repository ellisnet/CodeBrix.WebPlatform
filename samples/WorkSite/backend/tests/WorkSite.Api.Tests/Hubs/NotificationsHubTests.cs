using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SilverAssertions;
using WorkSite.Api.Hubs;
using WorkSite.Api.Tests.Infrastructure;
using WorkSite.Core.Hubs;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class NotificationsHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NotificationsHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task a_reminder_reaches_a_signed_in_connection_with_its_kind_as_a_number()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/notifications", client);
        var hub = _factory.Services.GetRequiredService<IHubContext<NotificationsHub, INotificationsClient>>();
        var sent = NotificationTicker.Reminder(1, new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero));

        //Act
        var received = await NotifyUntilHeardAsync(hub, connection, sent);

        //Assert
        received[0].GetProperty("id").GetString().Should().Be(sent.Id.ToString());
        received[0].GetProperty("kind").GetInt32().Should().Be((int)NotificationKind.Warning);
        received[0].GetProperty("message").GetString().Should().Be("The Northwind invoice is due on Friday.");
        received[0].GetProperty("sentAt").GetString().Should().StartWith("2026-11-18T10:00:00");
    }

    [Fact]
    public void Reminder_cycles_through_the_made_up_reminders()
    {
        //Arrange
        var at = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        //Act
        var reminders = Enumerable.Range(0, 12).Select(i => NotificationTicker.Reminder(i, at)).ToList();

        //Assert
        reminders.Select(r => r.Message).Distinct().Count().Should().Be(6);
        reminders[0].Message.Should().Be(reminders[6].Message);
        reminders.Select(r => r.Id).Distinct().Count().Should().Be(12);
        reminders.Select(r => r.Kind).Distinct().Should().BeEquivalentTo(new[] { NotificationKind.Info, NotificationKind.Warning, NotificationKind.Success });
        NotificationTicker.Reminder(-1, at).Message.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Sends a notification to all connections until the test connection hears it (for up to 5 s),
    /// and returns the arguments of the message that carries it. SignalR answers the handshake just
    /// before it adds the connection to the hub's connections, and this hub has no method whose
    /// answer would prove the adding, so a single send can reach nobody - on a slow machine it
    /// always did. A reminder of the app's own ticker that arrives meanwhile is passed over.
    /// </summary>
    private static async Task<JsonElement[]> NotifyUntilHeardAsync(IHubContext<NotificationsHub, INotificationsClient> hub,
        HubTestConnection connection, Notification sent)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            await hub.Clients.All.Notify(sent);
            try
            {
                while (true)
                {
                    var received = await connection.NextAsync("Notify", TimeSpan.FromMilliseconds(200));
                    if (received[0].GetProperty("id").GetString() == sent.Id.ToString())
                    {
                        return received;
                    }
                }
            }
            catch (TimeoutException) when (DateTime.UtcNow < deadline)
            {
                //Not heard yet: send again.
            }
        }
    }
}
