namespace WorkSite.Api.Configuration;

/// <summary>How access tokens are signed (configuration section <c>Jwt</c>).</summary>
public sealed class JwtSettings
{
    /// <summary>The HMAC-SHA256 signing key, at least 32 bytes as UTF-8 (<c>Jwt__SigningKey</c>, required).</summary>
    public string SigningKey { get; set; }

    /// <summary>The issuer and audience written into every token (<c>Jwt__Issuer</c>, default WorkSite).</summary>
    public string Issuer { get; set; } = "WorkSite";
}
