using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WorkSite.Api.Configuration;

namespace WorkSite.Api.Auth;

/// <summary>Issues the short-lived access tokens: HMAC-SHA256 JWTs carrying the user and session ids.</summary>
public sealed class AccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly TimeSpan _lifetime;

    /// <summary>Creates the issuer.</summary>
    /// <param name="settings">The signing key, issuer and token lifetime.</param>
    public AccessTokenIssuer(WorkSiteSettings settings)
    {
        _credentials = new SigningCredentials(CreateKey(settings.Jwt.SigningKey), SecurityAlgorithms.HmacSha256);
        _issuer = settings.Jwt.Issuer;
        _lifetime = TimeSpan.FromMinutes(settings.Auth.AccessTokenMinutes);
    }

    /// <summary>The signing key for a configured secret.</summary>
    /// <param name="signingKey">The secret.</param>
    /// <returns>The key.</returns>
    public static SymmetricSecurityKey CreateKey(string signingKey)
        => new(Encoding.UTF8.GetBytes(signingKey));

    /// <summary>Issues a token for a session.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="sessionId">The session.</param>
    /// <returns>The token and when it expires (UTC).</returns>
    public (string Token, DateTime ExpiresAt) Issue(Guid userId, Guid sessionId)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now + _lifetime;
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _issuer,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [AuthClaims.UserId] = userId.ToString(),
                [AuthClaims.SessionId] = sessionId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = _credentials,
        });
        return (token, expiresAt);
    }
}
