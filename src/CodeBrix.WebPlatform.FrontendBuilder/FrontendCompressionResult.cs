using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// What <see cref="PlatformFrontend.BuildAsync(FrontendConfiguration, string, System.Threading.CancellationToken)"/> compressed for one frontend application in the
/// built frontend folder (<see cref="FrontendBuildResult.Compression"/>; see
/// <see cref="PlatformFrontendOptions.CompressOutput"/>).
/// </summary>
public sealed class FrontendCompressionResult
{
    internal FrontendCompressionResult(FrontendCompression compression, IReadOnlyList<CompressedFile> files, int removedCopyCount, TimeSpan elapsed)
    {
        Compression = compression;
        Files = files;
        RemovedCopyCount = removedCopyCount;
        Elapsed = elapsed;
    }

    /// <summary>The copies that were asked for.</summary>
    public FrontendCompression Compression { get; }

    /// <summary>
    /// Every compressible file of the application, sorted by path, with the copies written
    /// beside it. Empty with <see cref="FrontendCompression.None"/>.
    /// </summary>
    public IReadOnlyList<CompressedFile> Files { get; }

    /// <summary>How many <c>.br</c> copies were written.</summary>
    public int BrotliFileCount => Files.Count(f => f.BrotliBytes.HasValue);

    /// <summary>How many <c>.gz</c> copies were written.</summary>
    public int GzipFileCount => Files.Count(f => f.GzipBytes.HasValue);

    /// <summary>The size of the compressible files themselves, in bytes.</summary>
    public long OriginalBytes => Files.Sum(f => f.OriginalBytes);

    /// <summary>
    /// What a browser that accepts Brotli downloads for those files, in bytes: each file's
    /// <c>.br</c> copy where one was written, the file itself otherwise.
    /// </summary>
    public long BrotliBytes => Files.Sum(f => f.BrotliBytes ?? f.OriginalBytes);

    /// <summary>
    /// What a browser that accepts gzip but not Brotli downloads for those files, in bytes:
    /// each file's <c>.gz</c> copy where one was written, the file itself otherwise.
    /// </summary>
    public long GzipBytes => Files.Sum(f => f.GzipBytes ?? f.OriginalBytes);

    /// <summary>
    /// How many <c>.br</c> / <c>.gz</c> files were removed: copies that the option does not ask
    /// for (or that are not smaller than their file), and copies whose file no longer exists.
    /// </summary>
    public int RemovedCopyCount { get; }

    /// <summary>The time the compression took.</summary>
    public TimeSpan Elapsed { get; }
}
