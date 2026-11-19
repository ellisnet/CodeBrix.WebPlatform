namespace WorkSite.DataAccess.Models;

/// <summary>The result of <c>ISessionRepository.RotateRefreshTokenAsync</c>.</summary>
public sealed class RefreshResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="outcome">What happened.</param>
    /// <param name="session">The session after rotation, or the revoked session; null when unknown.</param>
    public RefreshResult(RefreshOutcome outcome, Session session)
    {
        Outcome = outcome;
        Session = session;
    }

    /// <summary>What happened.</summary>
    public RefreshOutcome Outcome { get; }

    /// <summary>The session after rotation (for <see cref="RefreshOutcome.Rotated"/>), the revoked session
    /// (for <see cref="RefreshOutcome.Reused"/>), or null.</summary>
    public Session Session { get; }
}
