using System;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Auth;

/// <summary>
/// The two authentication cookies. Both are HttpOnly, Secure and SameSite=Strict; the access
/// cookie is sent to every API route, the refresh cookie only to the auth routes.
/// </summary>
public static class AuthCookies
{
    /// <summary>The access token cookie.</summary>
    public const string AccessCookie = "ws_access";

    /// <summary>The refresh token cookie.</summary>
    public const string RefreshCookie = "ws_refresh";

    /// <summary>The path of the access cookie.</summary>
    public const string AccessPath = "/api";

    /// <summary>The path of the refresh cookie.</summary>
    public const string RefreshPath = "/api/auth";

    /// <summary>Sets both cookies.</summary>
    /// <param name="response">The response.</param>
    /// <param name="accessToken">The access token.</param>
    /// <param name="accessExpiresAt">When the access token expires (UTC).</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="refreshExpiresAt">When the refresh token expires (UTC).</param>
    public static void Set(HttpResponse response, string accessToken, DateTime accessExpiresAt, string refreshToken,
        DateTime refreshExpiresAt)
    {
        response.Cookies.Append(AccessCookie, accessToken, Options(AccessPath, accessExpiresAt));
        response.Cookies.Append(RefreshCookie, refreshToken, Options(RefreshPath, refreshExpiresAt));
    }

    /// <summary>Tells the browser to drop both cookies.</summary>
    /// <param name="response">The response.</param>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(AccessCookie, Options(AccessPath, null));
        response.Cookies.Delete(RefreshCookie, Options(RefreshPath, null));
    }

    private static CookieOptions Options(string path, DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expiresAt.HasValue ? new DateTimeOffset(expiresAt.Value, TimeSpan.Zero) : null,
        IsEssential = true,
    };
}
