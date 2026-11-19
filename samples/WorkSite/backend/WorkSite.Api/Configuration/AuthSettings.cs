namespace WorkSite.Api.Configuration;

/// <summary>Sign-in lifetimes and password rules (configuration section <c>Auth</c>).</summary>
public sealed class AuthSettings
{
    /// <summary>How long an access token is valid (<c>Auth__AccessTokenMinutes</c>, default 15).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>How long a refresh token is valid (<c>Auth__RefreshTokenDays</c>, default 14).</summary>
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>The shortest password accepted (<c>Auth__MinimumPasswordLength</c>, default 6).</summary>
    public int MinimumPasswordLength { get; set; } = 6;

    /// <summary>The longest password accepted.</summary>
    public int MaximumPasswordLength { get; set; } = 200;
}
