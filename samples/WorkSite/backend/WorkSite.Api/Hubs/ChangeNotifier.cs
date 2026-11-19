using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Tells a user's connections that one of their records changed.</summary>
public sealed class ChangeNotifier
{
    private readonly IHubContext<ChangesHub, IChangesClient> _hub;

    /// <summary>Creates the notifier.</summary>
    /// <param name="hub">The hub's connections.</param>
    public ChangeNotifier(IHubContext<ChangesHub, IChangesClient> hub)
    {
        _hub = hub;
    }

    /// <summary>Sends a data-changed notice to every connection of a user.</summary>
    /// <param name="userId">The user whose data changed.</param>
    /// <param name="entity">The kind of record.</param>
    /// <param name="kind">What happened to it.</param>
    /// <param name="id">The record's id.</param>
    /// <returns>A task that completes when the notice is sent.</returns>
    public Task NotifyAsync(Guid userId, ChangedEntity entity, ChangeKind kind, Guid id)
        => _hub.Clients.User(HubUserIdProvider.IdOf(userId)).Changed(new DataChange { Entity = entity, Kind = kind, Id = id });
}
