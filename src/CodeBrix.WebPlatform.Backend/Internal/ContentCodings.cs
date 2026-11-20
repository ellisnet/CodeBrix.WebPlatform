using System;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Chooses the content coding of an answer from the request's <c>Accept-Encoding</c> (RFC 9110,
/// section 12.5.3), for the precompressed files and for the answers compressed as they are sent.
/// </summary>
internal static class ContentCodings
{
    /// <summary>The Brotli coding.</summary>
    public const string Brotli = "br";

    /// <summary>The gzip coding.</summary>
    public const string Gzip = "gzip";

    /// <summary>
    /// The coding to send, or <c>null</c> for none (the representation as it is). Each coding's
    /// weight is its own entry's q-value, otherwise that of <c>*</c>, otherwise 0 (<c>x-gzip</c>
    /// counts as <c>gzip</c>). A coding with weight 0 is refused. The heaviest available coding
    /// wins; on equal weights Brotli comes before gzip. The representation as it is ("identity")
    /// is always acceptable unless refused, but it wins over an acceptable coding only when it
    /// is listed itself (or through <c>*</c>) with a greater weight. When identity is refused
    /// and no available coding is acceptable, <c>null</c> is returned all the same: the
    /// representation is then sent as it is, as before (the server's choice under RFC 9110).
    /// </summary>
    /// <param name="acceptEncoding">The request's Accept-Encoding values.</param>
    /// <param name="brotliAvailable">Whether a Brotli representation can be sent.</param>
    /// <param name="gzipAvailable">Whether a gzip representation can be sent.</param>
    /// <returns><see cref="Brotli"/>, <see cref="Gzip"/> or <c>null</c>.</returns>
    public static string Choose(StringValues acceptEncoding, bool brotliAvailable, bool gzipAvailable)
    {
        if (StringValues.IsNullOrEmpty(acceptEncoding) || (!brotliAvailable && !gzipAvailable)
            || !StringWithQualityHeaderValue.TryParseList(acceptEncoding, out var entries) || entries.Count == 0)
        {
            return null;
        }

        double? brotli = null, gzip = null, identity = null, any = null;
        foreach (var entry in entries)
        {
            var quality = entry.Quality ?? 1.0;
            var name = entry.Value;
            if (name.Equals(Brotli, StringComparison.OrdinalIgnoreCase))
            {
                brotli = Math.Max(brotli ?? 0, quality);
            }
            else if (name.Equals(Gzip, StringComparison.OrdinalIgnoreCase) || name.Equals("x-gzip", StringComparison.OrdinalIgnoreCase))
            {
                gzip = Math.Max(gzip ?? 0, quality);
            }
            else if (name.Equals("identity", StringComparison.OrdinalIgnoreCase))
            {
                identity = Math.Max(identity ?? 0, quality);
            }
            else if (name.Equals("*", StringComparison.Ordinal))
            {
                any = Math.Max(any ?? 0, quality);
            }
        }

        var brotliWeight = brotliAvailable ? brotli ?? any ?? 0 : 0;
        var gzipWeight = gzipAvailable ? gzip ?? any ?? 0 : 0;
        var identityWeight = identity ?? any ?? 0;
        var best = brotliWeight >= gzipWeight ? brotliWeight : gzipWeight;
        if (best <= 0 || best < identityWeight)
        {
            return null;
        }

        return brotliWeight >= gzipWeight ? Brotli : Gzip;
    }
}
