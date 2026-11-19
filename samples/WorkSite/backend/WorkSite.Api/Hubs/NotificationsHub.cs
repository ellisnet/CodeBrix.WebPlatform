using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Reminders for the signed-in user, shown as toasts. They are made up and sent at irregular
/// intervals by <see cref="NotificationTicker"/>; the frontend calls nothing here. Needs sign-in.
/// </summary>
[WireHub("/api/hubs/notifications")]
[Authorize]
public sealed class NotificationsHub : Hub<INotificationsClient>
{
}
