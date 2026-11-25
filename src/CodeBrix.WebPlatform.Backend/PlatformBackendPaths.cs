namespace CodeBrix.WebPlatform.Backend;

/// <summary>The addresses and the fixed names the platform's rules are about.</summary>
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

    /// <summary>
    /// The name of the offline file. While a file of this name is in the web root
    /// (<c>wwwroot/</c>, the file at its top), the backend is OFFLINE; it looks for the file as
    /// requests arrive, so no restart is needed to go offline or to come back. While offline,
    /// every request below <c>/api</c> (hubs included) is answered 503 with the standard error
    /// body (<see cref="Problems.ApiProblems.Offline"/>); the developer's other files in
    /// <c>wwwroot/</c> are served as usual (the offline page may use its own stylesheet or
    /// pictures); every other request - each frontend application's pages and files, the built
    /// frontend's files, the backend's addresses outside <c>/api</c> - is answered 503 with the
    /// file's content as <c>text/html; charset=utf-8</c> and <c>Cache-Control: no-store</c>. The
    /// file is never served at its own address as an ordinary file. The name is fixed, and this
    /// works whatever the options say.
    /// </summary>
    public const string OfflineFileName = "app-offline.html";
}
