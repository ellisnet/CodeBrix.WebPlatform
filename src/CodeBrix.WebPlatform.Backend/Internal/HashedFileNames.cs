using System;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Tells the built frontend's files whose names carry dx's content hash. dx names such a file
/// <c>&lt;stem&gt;-dxh&lt;hash&gt;</c> followed by its extension (if any), the hash being the
/// eight bytes of a 64-bit hash, each written as lowercase hex WITHOUT a leading zero (so 8 to
/// 16 digits; dioxus-cli src/opt/hash.rs, add_hash_to_asset), or 16 digits for the split loader
/// (src/opt/js.rs, <c>__wasm_split-dxh{hash:016x}.js</c>). A file of such a name never changes
/// under it.
/// </summary>
internal static class HashedFileNames
{
    private const string Marker = "-dxh";

    /// <summary>True when the last segment of an address is a name dx gave a content hash.</summary>
    public static bool IsHashed(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var name = path.Substring(path.LastIndexOfAny(new[] { '/', '\\' }) + 1);
        var marker = name.LastIndexOf(Marker, StringComparison.Ordinal);
        if (marker <= 0)
        {
            return false;
        }

        var start = marker + Marker.Length;
        var dot = name.IndexOf('.', start);
        var end = dot < 0 ? name.Length : dot;
        var digits = end - start;
        if (digits < 8 || digits > 16)
        {
            return false;
        }

        for (var i = start; i < end; i++)
        {
            var c = name[i];
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        //At most one extension follows the hash, and it is not empty.
        return dot < 0 || (dot < name.Length - 1 && name.IndexOf('.', dot + 1) < 0);
    }
}
