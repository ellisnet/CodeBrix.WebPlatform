using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Data-changed notices: when a client, project or time entry is created, changed or deleted,
/// every connection of that user is told, so other open tabs can refresh. Needs sign-in. The
/// notices are sent by <see cref="ChangeNotifier"/>; the frontend calls nothing here.
/// </summary>
[WireHub("/api/hubs/changes")]
[Authorize]
public sealed class ChangesHub : Hub<IChangesClient>
{
}
