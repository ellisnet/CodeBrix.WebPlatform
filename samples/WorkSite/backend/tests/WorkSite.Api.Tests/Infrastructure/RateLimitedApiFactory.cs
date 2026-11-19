namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory whose sign-in, register and refresh routes allow three requests a minute.</summary>
public sealed class RateLimitedApiFactory : WorkSiteApiFactory
{
    /// <summary>Requests allowed per route and window.</summary>
    public const int PermitLimit = 3;

    /// <inheritdoc />
    protected override void Configure()
    {
        Set("RateLimiting:AuthPermitLimit", PermitLimit.ToString());
        Set("RateLimiting:AuthWindowSeconds", "60");
    }
}
