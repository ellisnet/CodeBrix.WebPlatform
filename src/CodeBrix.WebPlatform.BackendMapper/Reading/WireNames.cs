using System;
using System.Collections.Generic;
using System.Text;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>Turns .NET names into the names used on the wire and in Rust source.</summary>
internal static class WireNames
{
    private static readonly HashSet<string> RustKeywords = new(StringComparer.Ordinal)
    {
        "as", "async", "await", "break", "const", "continue", "dyn", "else", "enum", "extern", "false", "fn", "for",
        "if", "impl", "in", "let", "loop", "match", "mod", "move", "mut", "pub", "ref", "return", "static", "struct",
        "trait", "true", "type", "unsafe", "use", "where", "while", "abstract", "become", "box", "do", "final",
        "gen", "macro", "override", "priv", "try", "typeof", "unsized", "virtual", "yield",
    };

    //These cannot be written as raw identifiers.
    private static readonly HashSet<string> RustUnescapable = new(StringComparer.Ordinal) { "self", "Self", "super", "crate" };

    /// <summary>
    /// The camelCase form System.Text.Json's web defaults give a property name: the leading run of
    /// upper-case letters is lowered, except the last of them when a lower-case letter follows
    /// (<c>IsBillable</c> to <c>isBillable</c>, <c>URLValue</c> to <c>urlValue</c>, <c>ID</c> to <c>id</c>).
    /// </summary>
    public static string JsonCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 1 && !char.IsUpper(chars[i]))
            {
                break;
            }

            var hasNext = i + 1 < chars.Length;
            if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
            {
                if (chars[i + 1] == ' ')
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }

                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }

    /// <summary><c>IsBillable</c> to <c>is_billable</c>, <c>HTMLParser</c> to <c>html_parser</c>,
    /// <c>Export2Csv</c> to <c>export2_csv</c>. Characters that cannot be in an identifier become <c>_</c>.</summary>
    public static string SnakeCase(string name)
    {
        var text = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c))
            {
                if (text.Length > 0 && text[text.Length - 1] != '_')
                {
                    text.Append('_');
                }

                continue;
            }

            if (char.IsUpper(c))
            {
                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';
                var startsWord = i > 0 && char.IsLetterOrDigit(previous)
                    && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next)));
                if (startsWord && text.Length > 0 && text[text.Length - 1] != '_')
                {
                    text.Append('_');
                }

                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }

        var result = text.ToString().Trim('_');
        if (result.Length == 0)
        {
            return "_";
        }

        return char.IsDigit(result[0]) ? "_" + result : result;
    }

    /// <summary>The upper-case form of <see cref="SnakeCase"/>, for a Rust constant.</summary>
    public static string ScreamingSnakeCase(string name) => SnakeCase(name).ToUpperInvariant();

    /// <summary>A snake_case name as a Rust identifier, escaped when it is a keyword.</summary>
    public static string RustIdentifier(string snakeName)
    {
        if (RustUnescapable.Contains(snakeName))
        {
            return snakeName + "_";
        }

        return RustKeywords.Contains(snakeName) ? "r#" + snakeName : snakeName;
    }

    /// <summary>What serde's <c>rename_all = "camelCase"</c> makes of a snake_case field name.</summary>
    public static string SerdeCamelCase(string snakeName)
    {
        var text = new StringBuilder();
        var upperNext = false;
        foreach (var c in snakeName)
        {
            if (c == '_')
            {
                upperNext = text.Length > 0;
                continue;
            }

            text.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return text.ToString();
    }

    /// <summary>A .NET type name as a Rust type name: generic arity marks and punctuation removed.</summary>
    public static string TypeName(string clrSimpleName)
    {
        var tick = clrSimpleName.IndexOf('`');
        var name = tick >= 0 ? clrSimpleName.Substring(0, tick) : clrSimpleName;
        var text = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }

    /// <summary>Text as a Rust string literal's content.</summary>
    public static string RustString(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
