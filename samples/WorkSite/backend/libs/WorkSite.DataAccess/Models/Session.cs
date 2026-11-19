using System;

namespace WorkSite.DataAccess.Models;

/// <summary>One sign-in. Only hashes of refresh tokens are stored.</summary>
public sealed class Session
{
    /// <summary>The session id, carried in the access token.</summary>
    public Guid Id { get; set; }

    /// <summary>The signed-in user.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the current refresh token.</summary>
    public byte[] RefreshTokenHash { get; set; }

    /// <summary>SHA-256 hash of the refresh token the current one replaced, if any.</summary>
    public byte[] PreviousRefreshTokenHash { get; set; }

    /// <summary>When the user signed in (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the refresh token was last rotated (UTC).</summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>When the session ends unless it is refreshed (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the session was revoked (UTC), or null while it is live.</summary>
    public DateTime? RevokedAt { get; set; }
}
