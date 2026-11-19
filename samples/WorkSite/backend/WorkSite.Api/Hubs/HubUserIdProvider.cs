using System;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Api.Auth;

namespace WorkSite.Api.Hubs;

/// <summary>
/// Tells SignalR which user a connection belongs to (the access token's user id), so that a
/// message can be sent to every connection of one user.
/// </summary>
public sealed class HubUserIdProvider : IUserIdProvider
{
    /// <summary>A user id in the form SignalR addresses users by.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The id as text.</returns>
    public static string IdOf(Guid userId) => userId.ToString("D");

    /// <inheritdoc />
    public string GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var userId = AuthClaims.TryGetGuid(connection.User, AuthClaims.UserId);
        return userId.HasValue ? IdOf(userId.Value) : null;
    }
}
