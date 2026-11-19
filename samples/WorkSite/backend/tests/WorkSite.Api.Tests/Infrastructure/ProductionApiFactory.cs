namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory in the Production environment with a custom Content-Security-Policy.</summary>
public sealed class ProductionApiFactory : WorkSiteApiFactory
{
    /// <summary>The policy the factory configures.</summary>
    public const string ContentSecurityPolicy = "default-src 'self'; img-src 'self' https://images.example";

    /// <inheritdoc />
    protected override string EnvironmentName => "Production";

    /// <inheritdoc />
    protected override void Configure() => Set("Security:ContentSecurityPolicy", ContentSecurityPolicy);
}
