using System;
using System.Security.Claims;

namespace WorkSite.Api.Auth;

/// <summary>The claims an access token carries, and helpers to read them.</summary>
public static class AuthClaims
{
    /// <summary>The user id claim (JWT <c>sub</c>).</summary>
    public const string UserId = "sub";

    /// <summary>The session id claim (JWT <c>sid</c>).</summary>
    public const string SessionId = "sid";

    /// <summary>The signed-in user's id.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>The user id.</returns>
    /// <exception cref="InvalidOperationException">The principal carries no valid user id.</exception>
    public static Guid GetUserId(this ClaimsPrincipal principal)
        => TryGetGuid(principal, UserId) ?? throw new InvalidOperationException("The request is not signed in.");

    /// <summary>The id of the signed-in session, or null.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The session id, or null when not signed in.</returns>
    public static Guid? GetSessionId(this ClaimsPrincipal principal) => TryGetGuid(principal, SessionId);

    /// <summary>Reads a claim as a Guid.</summary>
    /// <param name="principal">The principal.</param>
    /// <param name="type">The claim type.</param>
    /// <returns>The value, or null when missing or not a Guid.</returns>
    public static Guid? TryGetGuid(ClaimsPrincipal principal, string type)
    {
        var value = principal?.FindFirst(type)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
