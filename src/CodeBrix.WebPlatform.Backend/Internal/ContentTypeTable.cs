using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// The content types of the served files, made once from
/// <see cref="PlatformBackendOptions.StaticFileContentTypes"/>: every <c>text/</c> type without a
/// charset gets <c>; charset=utf-8</c>.
/// </summary>
internal static class ContentTypeTable
{
    /// <summary>The charset parameter added to a text type that has none.</summary>
    public const string Utf8Charset = "utf-8";

    /// <summary>The content-type provider of the static files, from the options' table.</summary>
    /// <exception cref="InvalidOperationException">The table is null, or an entry is not an
    /// extension with its dot and a content type; the message says which.</exception>
    public static FileExtensionContentTypeProvider Create(IDictionary<string, string> table)
    {
        if (table == null)
        {
            throw new InvalidOperationException("PlatformBackendOptions.StaticFileContentTypes is null; change its entries instead of replacing it.");
        }

        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in table)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length < 2 || entry.Key[0] != '.' || entry.Key.IndexOfAny(new[] { '/', '\\', ' ' }) >= 0)
            {
                throw new InvalidOperationException(
                    $"PlatformBackendOptions.StaticFileContentTypes has the key \"{entry.Key}\", which is not a file extension; a key is an extension with its dot, for example \".wasm\".");
            }

            if (string.IsNullOrWhiteSpace(entry.Value) || !MediaTypeHeaderValue.TryParse(entry.Value, out var parsed) || !parsed.MediaType.HasValue
                || parsed.MediaType.Value.IndexOf('/') <= 0 || parsed.MatchesAllTypes || parsed.MatchesAllSubTypes)
            {
                throw new InvalidOperationException(
                    $"PlatformBackendOptions.StaticFileContentTypes gives the extension \"{entry.Key}\" the content type \"{entry.Value}\", which is not a content type such as \"application/wasm\".");
            }

            mappings[entry.Key] = WithUtf8Charset(parsed);
        }

        return new FileExtensionContentTypeProvider(mappings);
    }

    /// <summary>A content type, with <c>; charset=utf-8</c> added when it is a text type without a charset.</summary>
    public static string WithUtf8Charset(string contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var parsed) ? WithUtf8Charset(parsed) : contentType;

    private static string WithUtf8Charset(MediaTypeHeaderValue parsed)
    {
        if (parsed.Type.Equals("text", StringComparison.OrdinalIgnoreCase) && !parsed.Charset.HasValue)
        {
            parsed.Charset = Utf8Charset;
        }

        return parsed.ToString();
    }
}
