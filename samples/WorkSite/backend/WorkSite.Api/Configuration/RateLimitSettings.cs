namespace WorkSite.Api.Configuration;

/// <summary>Limits on the sign-in, register and refresh routes (configuration section <c>RateLimiting</c>).</summary>
public sealed class RateLimitSettings
{
    /// <summary>Requests allowed per client address and route in each window (<c>RateLimiting__AuthPermitLimit</c>, default 10).</summary>
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>The window length in seconds (<c>RateLimiting__AuthWindowSeconds</c>, default 60).</summary>
    public int AuthWindowSeconds { get; set; } = 60;
}
