using System;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Api.Auth;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Session-ended notices: when the user signs out, or changes their password in another browser,
/// the pages still open on the ended session are told and sign out at once. The notices are sent
/// by <see cref="SessionNotifier"/>; the frontend calls nothing here. Needs sign-in.
/// </summary>
[WireHub("/api/hubs/session")]
[Authorize]
public sealed class SessionHub : Hub<ISessionClient>
{
    private readonly SessionConnections _connections;

    /// <summary>Creates the hub.</summary>
    /// <param name="connections">Which session each connection belongs to.</param>
    public SessionHub(SessionConnections connections)
    {
        _connections = connections;
    }

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        var sessionId = Context.User.GetSessionId();
        if (sessionId.HasValue)
        {
            _connections.Add(Context.ConnectionId, Context.User.GetUserId(), sessionId.Value);
        }

        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception exception)
    {
        _connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
