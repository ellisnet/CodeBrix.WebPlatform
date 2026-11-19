using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Tells the pages of an ended session to sign out.</summary>
public sealed class SessionNotifier
{
    private readonly IHubContext<SessionHub, ISessionClient> _hub;
    private readonly SessionConnections _connections;

    /// <summary>Creates the notifier.</summary>
    /// <param name="hub">The hub's connections.</param>
    /// <param name="connections">Which session each connection belongs to.</param>
    public SessionNotifier(IHubContext<SessionHub, ISessionClient> hub, SessionConnections connections)
    {
        _hub = hub;
        _connections = connections;
    }

    /// <summary>Tells every page of a session that the user signed out.</summary>
    /// <param name="sessionId">The session that ended.</param>
    /// <returns>A task that completes when the notices are sent.</returns>
    public Task SignedOutAsync(Guid sessionId)
        => SendAsync(_connections.OfSession(sessionId), SessionEndReason.SignedOut);

    /// <summary>Tells the pages of a user's other sessions that the password changed.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="keepSessionId">The session the password was changed from, which stays signed in.</param>
    /// <returns>A task that completes when the notices are sent.</returns>
    public Task PasswordChangedAsync(Guid userId, Guid keepSessionId)
        => SendAsync(_connections.OfUserExcept(userId, keepSessionId), SessionEndReason.PasswordChanged);

    private Task SendAsync(IReadOnlyList<string> connectionIds, SessionEndReason reason)
        => connectionIds.Count == 0
            ? Task.CompletedTask
            : _hub.Clients.Clients(connectionIds).Ended(new SessionEnded { Reason = reason });
}
