namespace CodeBrix.WebPlatform.Backend;

/// <summary>The addresses the platform's rules are about.</summary>
public static class PlatformBackendPaths
{
    /// <summary>
    /// The prefix of every API address. Nothing below it is ever answered with a frontend
    /// application's <c>index.html</c>.
    /// </summary>
    public const string Api = "/api";

    /// <summary>
    /// The prefix of every hub's address. Every request below it must come from a page of this
    /// site (see <see cref="PlatformBackendOptions.RequireHubOrigin"/>). It is inside
    /// <see cref="Api"/> so that a session cookie scoped to <c>/api</c> reaches the hubs.
    /// </summary>
    public const string Hubs = "/api/hubs";
}
