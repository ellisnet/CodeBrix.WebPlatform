namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Which compressed copies <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/> writes beside the built
/// frontend files in the built frontend folder (<see cref="PlatformFrontendOptions.CompressOutput"/>). A
/// platform backend serves such a copy, instead of the file, to a browser that accepts it.
/// </summary>
public enum FrontendCompression
{
    /// <summary>
    /// No copies: the built frontend folder holds the files exactly as dx built them (the
    /// default for a debug build).
    /// </summary>
    None,

    /// <summary>A Brotli copy, <c>&lt;name&gt;.br</c>, beside each compressible file.</summary>
    Brotli,

    /// <summary>A gzip copy, <c>&lt;name&gt;.gz</c>, beside each compressible file.</summary>
    Gzip,

    /// <summary>
    /// Both copies beside each compressible file (the default for a release build): Brotli for
    /// every current browser, gzip for a client that does not take Brotli.
    /// </summary>
    BrotliAndGzip,
}
