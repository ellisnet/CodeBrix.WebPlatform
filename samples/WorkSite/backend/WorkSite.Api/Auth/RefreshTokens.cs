using System;
using System.Security.Cryptography;
using System.Text;

namespace WorkSite.Api.Auth;

/// <summary>Creates refresh tokens and the SHA-256 hashes that are stored in their place.</summary>
public static class RefreshTokens
{
    /// <summary>The number of random bytes in a token.</summary>
    public const int TokenBytes = 32;

    /// <summary>A new random token, base64url encoded.</summary>
    /// <returns>The token.</returns>
    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The SHA-256 hash of a token, the only form that is stored.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The 32-byte hash.</returns>
    public static byte[] Hash(string token)
        => SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty));
}
