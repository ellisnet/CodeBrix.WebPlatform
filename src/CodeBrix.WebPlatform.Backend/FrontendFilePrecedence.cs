namespace CodeBrix.WebPlatform.Backend;

/// <summary>
/// Which folder's file is served when the same address names a file in both the built frontend
/// (<c>frontend-built/</c>) and the developer's own web root (<c>wwwroot/</c>)
/// (<see cref="PlatformBackendOptions.FilePrecedence"/>). Either way the backend logs one warning
/// at start-up naming the clashing addresses.
/// </summary>
public enum FrontendFilePrecedence
{
    /// <summary>
    /// The built frontend's file is served and the <c>wwwroot/</c> file at the same address is
    /// hidden (the default): a stray <c>index.html</c> left in <c>wwwroot/</c> cannot silently
    /// hide an application.
    /// </summary>
    BuiltFrontendWins,

    /// <summary>
    /// The <c>wwwroot/</c> file is served and the built frontend's file at the same address is
    /// hidden, its compressed copies included: the developer's own file overrides the generated
    /// one, the root application's <c>index.html</c> (and so its fallback) included.
    /// </summary>
    WebRootWins,
}
