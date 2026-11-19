namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory whose wwwroot is empty, as before the frontend has been built.</summary>
public sealed class EmptyWebRootApiFactory : WorkSiteApiFactory
{
    /// <inheritdoc />
    protected override bool WithFrontendFiles => false;
}
