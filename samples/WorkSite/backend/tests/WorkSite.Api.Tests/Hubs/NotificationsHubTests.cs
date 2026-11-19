using System;
using System.Linq;
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
        await hub.Clients.All.Notify(sent);
        var received = await connection.NextAsync("Notify", TimeSpan.FromSeconds(5));

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
}
