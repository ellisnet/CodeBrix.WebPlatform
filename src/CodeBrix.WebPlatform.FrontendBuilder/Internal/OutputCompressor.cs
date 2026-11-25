using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Writes the compressed copies of a built frontend application's files in the built frontend
/// folder (<see cref="PlatformFrontendOptions.CompressOutput"/>): <c>&lt;name&gt;.br</c> (Brotli,
/// quality 11, window 24) and <c>&lt;name&gt;.gz</c> (gzip, smallest size) beside each file
/// whose extension is in <see cref="PlatformFrontendOptions.CompressibleExtensions"/>, only when
/// the copy is smaller. The same file always gives the same bytes. A <c>.gitignore</c> is never
/// compressed or counted.
/// </summary>
internal static class OutputCompressor
{
    /// <summary>The suffix of a Brotli copy.</summary>
    internal const string BrotliSuffix = ".br";

    /// <summary>The suffix of a gzip copy.</summary>
    internal const string GzipSuffix = ".gz";

    /// <summary>Brotli's strongest quality.</summary>
    internal const int BrotliQuality = 11;

    /// <summary>Brotli's largest window (log2 of its size).</summary>
    internal const int BrotliWindow = 24;

    /// <summary>
    /// True when a file of that name is compressed: its extension is in the set, and it is not a
    /// <c>.gitignore</c> (a <c>.br</c> / <c>.gz</c> name is never in the set, see
    /// <see cref="RequireExtensions"/>).
    /// </summary>
    /// <param name="fileName">The file's name.</param>
    /// <param name="extensions">The compressible extensions, with the dot (compared ignoring case).</param>
    internal static bool IsCompressible(string fileName, IReadOnlyCollection<string> extensions)
        => !string.IsNullOrEmpty(fileName) && !BuildOutputFolder.IsIgnoreFile(fileName) && extensions.Contains(Path.GetExtension(fileName));

    /// <summary>
    /// Checks a set of compressible extensions and returns it as a set that ignores case.
    /// </summary>
    /// <param name="extensions">The extensions (<see cref="PlatformFrontendOptions.CompressibleExtensions"/>).</param>
    /// <returns>The set.</returns>
    /// <exception cref="FrontendBuildException">An entry is not an extension (null, blank, not
    /// starting with a dot, with a further dot or a path separator), or is <c>.br</c> / <c>.gz</c>.</exception>
    internal static IReadOnlyCollection<string> RequireExtensions(IEnumerable<string> extensions)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(extension) || extension.Length < 2 || extension[0] != '.' || extension.IndexOf('.', 1) >= 0
                || extension.IndexOfAny(new[] { '/', '\\', ' ' }) >= 0)
            {
                throw new FrontendBuildException(
                    $"PlatformFrontendOptions.CompressibleExtensions holds \"{extension}\", which is not a file extension. Each entry is one extension with its dot, such as \".wasm\".");
            }

            if (IsCopyName(extension))
            {
                throw new FrontendBuildException(
                    $"PlatformFrontendOptions.CompressibleExtensions holds \"{extension}\": a {BrotliSuffix} or {GzipSuffix} file is a compressed copy already. Remove it from the set.");
            }

            set.Add(extension);
        }

        return set;
    }

    /// <summary>True when the name ends in <c>.br</c> or <c>.gz</c>.</summary>
    internal static bool IsCopyName(string fileName)
        => fileName.EndsWith(BrotliSuffix, StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(GzipSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the copies for the files of one application. A <c>.br</c> / <c>.gz</c> file of the
    /// application that sits beside its own file (one dx wrote, with dx's pre-compression on)
    /// is a copy: it is replaced by this library's, or removed when the option does not ask for
    /// it - except with <see cref="FrontendCompression.None"/>, which writes and replaces
    /// nothing. In the folders that hold the application's files, a <c>.br</c> / <c>.gz</c>
    /// file that is not one of them and whose file no longer exists is a stale copy and is
    /// removed, whatever the option. A <c>.br</c> / <c>.gz</c> file of the application with no
    /// file beside it is a file in its own right and is left alone.
    /// </summary>
    /// <param name="applicationDirectory">The application's folder in the built frontend folder.</param>
    /// <param name="relativeFiles">The application's files, relative to that folder.</param>
    /// <param name="compression">The copies to write.</param>
    /// <param name="extensions">The compressible extensions (from <see cref="RequireExtensions"/>).</param>
    /// <param name="cancellationToken">Stops the work between files.</param>
    /// <returns>What was written and removed.</returns>
    /// <exception cref="FrontendBuildException">A file could not be read or a copy could not be
    /// written.</exception>
    internal static FrontendCompressionResult Compress(string applicationDirectory, IEnumerable<string> relativeFiles, FrontendCompression compression, IReadOnlyCollection<string> extensions, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var files = new HashSet<string>(relativeFiles.Select(f => f.Replace('\\', '/')).Where(f => !BuildOutputFolder.IsIgnoreFile(f)), StringComparer.Ordinal);
        var copies = files.Where(f => IsCopyName(f) && files.Contains(f.Substring(0, f.Length - 3))).ToHashSet(StringComparer.Ordinal);
        var originals = files.Where(f => !copies.Contains(f) && IsCompressible(Path.GetFileName(f), extensions)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        var removed = 0;
        var results = new List<CompressedFile>();
        if (compression != FrontendCompression.None)
        {
            var writeBrotli = compression == FrontendCompression.Brotli || compression == FrontendCompression.BrotliAndGzip;
            var writeGzip = compression == FrontendCompression.Gzip || compression == FrontendCompression.BrotliAndGzip;
            var done = new CompressedFile[originals.Count];
            try
            {
                Parallel.For(0, originals.Count, new ParallelOptions { CancellationToken = cancellationToken }, i =>
                {
                    var path = Full(applicationDirectory, originals[i]);
                    try
                    {
                        var bytes = File.ReadAllBytes(path);
                        var brotli = WriteOrRemove(path + BrotliSuffix, writeBrotli ? Brotli(bytes) : null, bytes.Length, ref removed);
                        var gzip = WriteOrRemove(path + GzipSuffix, writeGzip ? Gzip(bytes) : null, bytes.Length, ref removed);
                        done[i] = new CompressedFile(originals[i], bytes.Length, brotli, gzip);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        throw new FrontendBuildException(
                            $"The compressed copies of {path} could not be written: {e.Message} Check that nothing else holds the built frontend folder's files open, or set PlatformFrontendOptions.CompressOutput to None.", e);
                    }
                });
            }
            catch (AggregateException e) when (e.InnerExceptions.Count > 0 && e.InnerExceptions[0] is FrontendBuildException)
            {
                throw e.InnerExceptions[0];
            }

            results.AddRange(done);
        }

        removed += RemoveStaleCopies(applicationDirectory, files);
        return new FrontendCompressionResult(compression, results, removed, stopwatch.Elapsed);
    }

    /// <summary>The Brotli copy of some bytes: quality 11, window 24.</summary>
    internal static byte[] Brotli(byte[] bytes)
    {
        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(bytes.Length)];
        if (!BrotliEncoder.TryCompress(bytes, buffer, out var written, BrotliQuality, BrotliWindow))
        {
            throw new IOException("Brotli could not compress the file.");
        }

        return buffer.AsSpan(0, written).ToArray();
    }

    /// <summary>
    /// The gzip copy of some bytes, at the smallest size. The header's operating-system byte is
    /// set to 255 ("unknown"), so that every machine writes the same bytes.
    /// </summary>
    internal static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        var result = output.ToArray();
        if (result.Length >= 10)
        {
            result[9] = 0xff;
        }

        return result;
    }

    /// <summary>
    /// Writes the copy when there is one and it is smaller than the file; otherwise removes a
    /// copy that is there. Returns the size written, or null.
    /// </summary>
    private static long? WriteOrRemove(string copyPath, byte[] copy, long originalLength, ref int removed)
    {
        if (copy != null && copy.Length < originalLength)
        {
            File.WriteAllBytes(copyPath, copy);
            return copy.Length;
        }

        if (File.Exists(copyPath))
        {
            File.Delete(copyPath);
            Interlocked.Increment(ref removed);
        }

        return null;
    }

    /// <summary>
    /// Removes, in each folder that holds a file of the application, the <c>.br</c> and
    /// <c>.gz</c> files that are not the application's own and whose file no longer exists.
    /// </summary>
    private static int RemoveStaleCopies(string applicationDirectory, IReadOnlyCollection<string> files)
    {
        var removed = 0;
        var folders = files.Select(f => Path.GetDirectoryName(Full(applicationDirectory, f))).Distinct(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var candidate in Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileName(candidate);
                if (!IsCopyName(name))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(applicationDirectory, candidate).Replace('\\', '/');
                var original = candidate.Substring(0, candidate.Length - 3);
                if (!files.Contains(relative) && !File.Exists(original) && !Directory.Exists(original))
                {
                    File.Delete(candidate);
                    removed++;
                }
            }
        }

        return removed;
    }

    private static string Full(string applicationDirectory, string relative)
        => Path.Combine(applicationDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
}
