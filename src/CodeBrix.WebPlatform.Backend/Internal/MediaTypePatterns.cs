using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// A set of content types made from <see cref="PlatformBackendOptions.CompressedFileTypes"/>:
/// each entry is a type (<c>application/wasm</c>), every subtype of a type (<c>text/*</c>) or
/// every subtype with a structured-syntax suffix (<c>*/*+xml</c>, <c>image/*+xml</c>), compared
/// without regard to case.
/// </summary>
internal sealed class MediaTypePatterns
{
    private readonly (string Type, string Subtype)[] _patterns;

    private MediaTypePatterns((string Type, string Subtype)[] patterns) => _patterns = patterns;

    /// <summary>Reads the entries of an option.</summary>
    /// <exception cref="InvalidOperationException">The set is null or an entry cannot be read; the message says which.</exception>
    public static MediaTypePatterns Parse(IEnumerable<string> entries, string optionName)
    {
        if (entries == null)
        {
            throw new InvalidOperationException($"PlatformBackendOptions.{optionName} is null; change its entries instead of replacing it.");
        }

        var patterns = new List<(string, string)>();
        foreach (var entry in entries)
        {
            var text = entry?.Trim().ToLowerInvariant();
            var slash = text?.IndexOf('/') ?? -1;
            var type = slash > 0 ? text.Substring(0, slash) : null;
            var subtype = slash > 0 ? text.Substring(slash + 1) : null;
            if (type == null || subtype.Length == 0 || text.IndexOfAny(new[] { ';', ' ', ',' }) >= 0 || subtype.Contains('/')
                || (type == "*" && !subtype.StartsWith("*+", StringComparison.Ordinal))
                || (subtype.Contains('*') && subtype != "*" && !(subtype.StartsWith("*+", StringComparison.Ordinal) && subtype.Length > 2 && subtype.IndexOf('*', 1) < 0)))
            {
                throw new InvalidOperationException(
                    $"PlatformBackendOptions.{optionName} has the entry \"{entry}\", which is not a content type (\"application/wasm\"), every subtype of a type (\"text/*\") or a structured-syntax suffix (\"*/*+xml\").");
            }

            patterns.Add((type, subtype));
        }

        return new MediaTypePatterns(patterns.ToArray());
    }

    /// <summary>True when a content type (its parameters ignored) is in the set.</summary>
    public bool Matches(string contentType)
    {
        if (string.IsNullOrEmpty(contentType) || !MediaTypeHeaderValue.TryParse(contentType, out var parsed) || !parsed.MediaType.HasValue)
        {
            return false;
        }

        var text = parsed.MediaType.Value.ToLowerInvariant();
        var slash = text.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        var type = text.Substring(0, slash);
        var subtype = text.Substring(slash + 1);
        return _patterns.Any(p => (p.Type == "*" || p.Type == type) && SubtypeMatches(p.Subtype, subtype));
    }

    private static bool SubtypeMatches(string pattern, string subtype)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (pattern.StartsWith("*+", StringComparison.Ordinal))
        {
            return subtype.Length > pattern.Length - 1 && subtype.EndsWith(pattern.Substring(1), StringComparison.Ordinal);
        }

        return pattern == subtype;
    }
}
