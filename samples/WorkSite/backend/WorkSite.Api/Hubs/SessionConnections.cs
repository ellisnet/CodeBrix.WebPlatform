using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WorkSite.Api.Hubs;

/// <summary>Which sign-in session each connection of the session hub belongs to.</summary>
public sealed class SessionConnections
{
    private readonly ConcurrentDictionary<string, (Guid UserId, Guid SessionId)> _connections = new(StringComparer.Ordinal);

    /// <summary>Records a new connection.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="sessionId">The sign-in session.</param>
    public void Add(string connectionId, Guid userId, Guid sessionId) => _connections[connectionId] = (userId, sessionId);

    /// <summary>Forgets a connection that has closed.</summary>
    /// <param name="connectionId">The connection.</param>
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>The connections of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The connection ids.</returns>
    public IReadOnlyList<string> OfSession(Guid sessionId)
        => _connections.Where(c => c.Value.SessionId == sessionId).Select(c => c.Key).ToList();

    /// <summary>The connections of a user that belong to any session but one.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="keepSessionId">The session to leave out.</param>
    /// <returns>The connection ids.</returns>
    public IReadOnlyList<string> OfUserExcept(Guid userId, Guid keepSessionId)
        => _connections.Where(c => c.Value.UserId == userId && c.Value.SessionId != keepSessionId).Select(c => c.Key).ToList();
}
