namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// One compressible file of a built frontend application in the web root, and the compressed
/// copies <see cref="PlatformFrontend.BuildAsync"/> wrote beside it (part of
/// <see cref="FrontendCompressionResult"/>).
/// </summary>
public sealed class CompressedFile
{
    internal CompressedFile(string path, long originalBytes, long? brotliBytes, long? gzipBytes)
    {
        Path = path;
        OriginalBytes = originalBytes;
        BrotliBytes = brotliBytes;
        GzipBytes = gzipBytes;
    }

    /// <summary>
    /// The file's path inside the application's folder of the web root
    /// (<see cref="FrontendBuildResult.WebRootApplicationDirectory"/>), with forward slashes, for
    /// example <c>wasm/app_bg.wasm</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>The size of the file itself, in bytes.</summary>
    public long OriginalBytes { get; }

    /// <summary>
    /// The size of its <c>.br</c> copy, or <c>null</c> when none was written (not asked for, or
    /// not smaller than the file).
    /// </summary>
    public long? BrotliBytes { get; }

    /// <summary>
    /// The size of its <c>.gz</c> copy, or <c>null</c> when none was written (not asked for, or
    /// not smaller than the file).
    /// </summary>
    public long? GzipBytes { get; }

    /// <summary>A line fit to show: the path and the sizes.</summary>
    /// <returns>For example <c>wasm/app_bg.wasm 1048576 bytes, br 301234, gz 402345</c>.</returns>
    public override string ToString()
        => Path + " " + OriginalBytes + " bytes"
            + (BrotliBytes.HasValue ? ", br " + BrotliBytes.Value : string.Empty)
            + (GzipBytes.HasValue ? ", gz " + GzipBytes.Value : string.Empty);
}
