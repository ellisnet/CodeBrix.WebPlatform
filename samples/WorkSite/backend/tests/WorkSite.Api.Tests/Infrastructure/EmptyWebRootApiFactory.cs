namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory with no built frontend (no frontend-built/), as before the frontend has been built.</summary>
public sealed class EmptyWebRootApiFactory : WorkSiteApiFactory
{
    /// <inheritdoc />
    protected override bool WithFrontendFiles => false;
}
