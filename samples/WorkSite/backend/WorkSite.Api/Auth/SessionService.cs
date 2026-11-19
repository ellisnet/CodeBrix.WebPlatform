using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WorkSite.Api.Configuration;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Auth;

/// <summary>
/// Starts, refreshes and ends sign-in sessions, keeping the session rows and the two cookies in
/// step. A refresh rotates the refresh token; presenting a token that was already replaced
/// revokes the whole session.
/// </summary>
public sealed class SessionService
{
    private readonly ISessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly AccessTokenIssuer _issuer;
    private readonly TimeSpan _refreshLifetime;

    /// <summary>Creates the service.</summary>
    /// <param name="sessions">The session store.</param>
    /// <param name="users">The user store.</param>
    /// <param name="issuer">Issues access tokens.</param>
    /// <param name="settings">The refresh token lifetime.</param>
    public SessionService(ISessionRepository sessions, IUserRepository users, AccessTokenIssuer issuer,
        WorkSiteSettings settings)
    {
        _sessions = sessions;
        _users = users;
        _issuer = issuer;
        _refreshLifetime = TimeSpan.FromDays(settings.Auth.RefreshTokenDays);
    }

    /// <summary>Signs a user in: creates a session and sets both cookies.</summary>
    /// <param name="response">The response that carries the cookies.</param>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new session.</returns>
    public async Task<Session> StartAsync(HttpResponse response, Guid userId, CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokens.Generate();
        var expiresAt = DateTime.UtcNow + _refreshLifetime;
        var session = await _sessions.CreateAsync(userId, RefreshTokens.Hash(refreshToken), expiresAt, cancellationToken);
        var (accessToken, accessExpiresAt) = _issuer.Issue(userId, session.Id);
        AuthCookies.Set(response, accessToken, accessExpiresAt, refreshToken, expiresAt);
        return session;
    }

    /// <summary>
    /// Exchanges the refresh cookie for new cookies. Returns the user, or null (with the cookies
    /// cleared) when there is no valid refresh token.
    /// </summary>
    /// <param name="context">The request and response.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The signed-in user, or null.</returns>
    public async Task<User> RefreshAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var presented = context.Request.Cookies[AuthCookies.RefreshCookie];
        if (string.IsNullOrEmpty(presented))
        {
            AuthCookies.Clear(context.Response);
            return null;
        }

        var refreshToken = RefreshTokens.Generate();
        var expiresAt = DateTime.UtcNow + _refreshLifetime;
        var result = await _sessions.RotateRefreshTokenAsync(RefreshTokens.Hash(presented), RefreshTokens.Hash(refreshToken),
            expiresAt, cancellationToken);
        if (result.Outcome != RefreshOutcome.Rotated)
        {
            AuthCookies.Clear(context.Response);
            return null;
        }

        var user = await _users.GetByIdAsync(result.Session.UserId, cancellationToken);
        if (user == null)
        {
            await _sessions.RevokeAsync(result.Session.Id, cancellationToken);
            AuthCookies.Clear(context.Response);
            return null;
        }

        var (accessToken, accessExpiresAt) = _issuer.Issue(user.Id, result.Session.Id);
        AuthCookies.Set(context.Response, accessToken, accessExpiresAt, refreshToken, expiresAt);
        return user;
    }

    /// <summary>
    /// Signs out: revokes the session named by the access token and the one holding the refresh
    /// cookie, and clears both cookies. Never fails, even when nobody is signed in.
    /// </summary>
    /// <param name="context">The request and response.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the session is revoked.</returns>
    public async Task EndAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = context.User.GetSessionId();
        if (sessionId.HasValue)
        {
            await _sessions.RevokeAsync(sessionId.Value, cancellationToken);
        }

        var refreshToken = context.Request.Cookies[AuthCookies.RefreshCookie];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _sessions.RevokeByRefreshTokenHashAsync(RefreshTokens.Hash(refreshToken), cancellationToken);
        }

        AuthCookies.Clear(context.Response);
    }
}
