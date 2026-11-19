namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory with the demo account enabled.</summary>
public sealed class DemoApiFactory : WorkSiteApiFactory
{
    /// <summary>The demo username.</summary>
    public const string Username = "demo";

    /// <summary>The demo password the factory configures.</summary>
    public const string Password = "demo-password-for-tests";

    /// <inheritdoc />
    protected override void Configure()
    {
        Set("Demo:Enabled", "true");
        Set("Demo:Username", Username);
        Set("Demo:Password", Password);
    }
}
