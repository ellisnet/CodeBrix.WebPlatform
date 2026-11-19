namespace WorkSite.DataAccess.Models;

/// <summary>What happened when a refresh token was presented.</summary>
public enum RefreshOutcome
{
    /// <summary>The token was current; it has been replaced by the new one.</summary>
    Rotated,

    /// <summary>The token had already been replaced, so the whole session has been revoked.</summary>
    Reused,

    /// <summary>The token is unknown, or its session is revoked or expired.</summary>
    Invalid,
}
