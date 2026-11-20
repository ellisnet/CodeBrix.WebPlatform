namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// Which compressed copies <see cref="PlatformFrontend.BuildAsync"/> writes beside the built
/// frontend files in the web root (<see cref="PlatformFrontendOptions.CompressOutput"/>). A
/// platform backend serves such a copy, instead of the file, to a browser that accepts it.
/// </summary>
public enum FrontendCompression
{
    /// <summary>No copies: the web root holds the files exactly as dx built them.</summary>
    None,

    /// <summary>A Brotli copy, <c>&lt;name&gt;.br</c>, beside each compressible file.</summary>
    Brotli,

    /// <summary>A gzip copy, <c>&lt;name&gt;.gz</c>, beside each compressible file.</summary>
    Gzip,

    /// <summary>
    /// Both copies beside each compressible file (the default): Brotli for every current
    /// browser, gzip for a client that does not take Brotli.
    /// </summary>
    BrotliAndGzip,
}
