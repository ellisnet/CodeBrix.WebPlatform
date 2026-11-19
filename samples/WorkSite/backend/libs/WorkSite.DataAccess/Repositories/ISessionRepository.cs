using System;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Reads and writes sign-in sessions. Refresh tokens are handled only as SHA-256 hashes.</summary>
public interface ISessionRepository
{
    /// <summary>Starts a session.</summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="refreshTokenHash">The hash of the session's first refresh token.</param>
    /// <param name="expiresAt">When the session ends unless refreshed (UTC).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new session.</returns>
    Task<Session> CreateAsync(Guid userId, byte[] refreshTokenHash, DateTime expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a session by id.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The session, or null.</returns>
    Task<Session> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Whether the session exists for this user and is neither revoked nor expired.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="userId">The user the session must belong to.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True for a live session.</returns>
    Task<bool> IsLiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a presented refresh token for a new one. A current token of a live session is
    /// rotated (its hash kept as the previous hash); a token that was already replaced revokes the
    /// whole session; anything else is invalid.
    /// </summary>
    /// <param name="presentedHash">The hash of the presented refresh token.</param>
    /// <param name="newHash">The hash of the replacement token.</param>
    /// <param name="newExpiresAt">The session's new expiry (UTC).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What happened.</returns>
    Task<RefreshResult> RotateRefreshTokenAsync(byte[] presentedHash, byte[] newHash, DateTime newExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a session.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when a live session was revoked.</returns>
    Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Revokes the session whose current or previous refresh token has this hash.</summary>
    /// <param name="refreshTokenHash">The refresh token hash.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when a live session was revoked.</returns>
    Task<bool> RevokeByRefreshTokenHashAsync(byte[] refreshTokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes every live session of the user except one.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="keepSessionId">The session to keep.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sessions were revoked.</returns>
    Task<int> RevokeOtherSessionsAsync(Guid userId, Guid keepSessionId, CancellationToken cancellationToken = default);
}
