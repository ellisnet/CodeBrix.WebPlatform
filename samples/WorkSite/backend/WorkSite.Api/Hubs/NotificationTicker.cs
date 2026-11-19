using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Sends a made-up reminder to every signed-in connection every twenty to forty-five seconds.</summary>
public sealed class NotificationTicker : BackgroundService
{
    private static readonly (NotificationKind Kind, string Message)[] Reminders =
    [
        (NotificationKind.Info, "You have not logged any time in the last hour."),
        (NotificationKind.Warning, "The Northwind invoice is due on Friday."),
        (NotificationKind.Success, "Last week's timesheet was approved."),
        (NotificationKind.Info, "Three projects have had no hours this week."),
        (NotificationKind.Warning, "You are at 90% of the hours budgeted for the website redesign."),
        (NotificationKind.Success, "Your weekly summary is ready on the Reports page."),
    ];

    private readonly IHubContext<NotificationsHub, INotificationsClient> _hub;
    private readonly TimeProvider _time;

    /// <summary>Creates the ticker.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="time">The clock.</param>
    public NotificationTicker(IHubContext<NotificationsHub, INotificationsClient> hub, TimeProvider time)
    {
        _hub = hub;
        _time = time;
    }

    /// <summary>The reminder with a given number (the reminders repeat).</summary>
    /// <param name="number">Any number.</param>
    /// <param name="sentAt">When it is sent.</param>
    /// <returns>The reminder.</returns>
    public static Notification Reminder(int number, DateTimeOffset sentAt)
    {
        var (kind, message) = Reminders[(int)((uint)number % (uint)Reminders.Length)];
        return new Notification { Id = Guid.CreateVersion7(), Kind = kind, Message = message, SentAt = sentAt.UtcDateTime };
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var number = Random.Shared.Next(Reminders.Length);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(20, 46)), _time, stoppingToken);
            await _hub.Clients.All.Notify(Reminder(number++, _time.GetUtcNow()));
        }
    }
}
