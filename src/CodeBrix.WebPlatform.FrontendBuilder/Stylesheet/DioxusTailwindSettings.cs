using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;

/// <summary>
/// Refuses a frontend whose <c>Dioxus.toml</c> moves the Tailwind stylesheets: the platform's
/// paths are fixed (the input <c>tailwind.css</c> and the generated
/// <c>assets/generated/tailwind.css</c>, both in the frontend folder), and dx would otherwise
/// read and write other files than the ones the library generates and the backend expects. A
/// setting that names exactly the fixed path is accepted. dx reads the file from the frontend
/// folder (as <c>Dioxus.toml</c> or <c>dioxus.toml</c>) and takes a relative path from that folder.
/// </summary>
internal static class DioxusTailwindSettings
{
    /// <summary>The names dx looks for its configuration under, in its order.</summary>
    internal static readonly string[] FileNames = { "Dioxus.toml", "dioxus.toml" };

    /// <summary>dx's setting for the Tailwind input, in its <c>[application]</c> table.</summary>
    internal const string InputKey = "tailwind_input";

    /// <summary>dx's setting for the generated stylesheet, in its <c>[application]</c> table.</summary>
    internal const string OutputKey = "tailwind_output";

    /// <summary>The fixed input, relative to the frontend folder.</summary>
    internal const string FixedInput = "tailwind.css";

    /// <summary>The fixed generated stylesheet, relative to the frontend folder.</summary>
    internal const string FixedOutput = "assets/generated/tailwind.css";

    private const string Table = "application";

    /// <summary>
    /// Says what is wrong with the Tailwind settings of a frontend's dx configuration, or
    /// <c>null</c> when it has none that differ from the fixed paths (or has no configuration).
    /// </summary>
    /// <param name="frontendDirectory">The frontend folder, as a full path.</param>
    internal static string DescribeProblem(string frontendDirectory)
    {
        var path = FileNames.Select(n => Path.Combine(frontendDirectory, n)).FirstOrDefault(File.Exists);
        if (path == null)
        {
            return null;
        }

        var wrong = Read(File.ReadAllText(path, Encoding.UTF8))
            .Where(s => !NamesFixedPath(frontendDirectory, s.Value, s.Key == InputKey ? FixedInput : FixedOutput))
            .ToList();
        if (wrong.Count == 0)
        {
            return null;
        }

        return $"'{path}' sets " + string.Join(" and ", wrong.Select(s => $"{s.Key} = {s.Value}"))
            + ". The platform's Tailwind paths are fixed - the input is tailwind.css and the generated stylesheet is assets/generated/tailwind.css, both in the frontend folder - so "
            + (wrong.Count == 1 ? "that setting" : "those settings") + " cannot be used: remove "
            + (wrong.Count == 1 ? "it" : "them") + " from the file (and move the stylesheet there if it is somewhere else).";
    }

    /// <summary>
    /// The Tailwind settings in the text of a dx configuration: each key of
    /// <c>[application]</c> named <see cref="InputKey"/> or <see cref="OutputKey"/>, in a table
    /// header, as a dotted key or in an inline table, with its value as written (a string keeps
    /// its quotes).
    /// </summary>
    /// <param name="text">The file's text.</param>
    internal static IReadOnlyList<(string Key, string Value)> Read(string text)
    {
        var settings = new List<(string Key, string Value)>();
        var table = string.Empty;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                table = NormalizeKey(line.Trim('[', ']'));
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = NormalizeKey(line.Substring(0, equals));
            var value = line.Substring(equals + 1).Trim();
            var full = table.Length == 0 ? key : table + "." + key;
            if (full == Table && value.StartsWith('{'))
            {
                foreach (var pair in SplitInlineTable(value))
                {
                    var inner = pair.IndexOf('=');
                    if (inner > 0)
                    {
                        Add(settings, Table + "." + NormalizeKey(pair.Substring(0, inner)), pair.Substring(inner + 1).Trim());
                    }
                }

                continue;
            }

            Add(settings, full, value);
        }

        return settings;
    }

    private static void Add(List<(string Key, string Value)> settings, string fullKey, string value)
    {
        if (fullKey == Table + "." + InputKey)
        {
            settings.Add((InputKey, value));
        }
        else if (fullKey == Table + "." + OutputKey)
        {
            settings.Add((OutputKey, value));
        }
    }

    /// <summary>True when a setting's value names the fixed path (relative to the frontend folder, or in full).</summary>
    private static bool NamesFixedPath(string frontendDirectory, string value, string fixedPath)
    {
        if (value.Length < 2 || (value[0] != '"' && value[0] != '\'') || value[^1] != value[0])
        {
            return false;
        }

        var path = value.Substring(1, value.Length - 2);
        if (value[0] == '"')
        {
            path = path.Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (path.Length == 0)
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(frontendDirectory, path.Replace('\\', '/')));
            var expected = Path.GetFullPath(Path.Combine(frontendDirectory, fixedPath));
            return string.Equals(full, expected, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string NormalizeKey(string key)
        => key.Replace("\"", string.Empty, StringComparison.Ordinal).Replace("'", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\t", string.Empty, StringComparison.Ordinal);

    private static IEnumerable<string> SplitInlineTable(string value)
    {
        var body = value.Trim().TrimStart('{').TrimEnd('}');
        var current = new StringBuilder();
        char quote = '\0';
        foreach (var c in body)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
            }
            else if (c == ',')
            {
                yield return current.ToString().Trim();
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            yield return current.ToString().Trim();
        }
    }

    private static string StripComment(string line)
    {
        char quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote != '\0')
            {
                if (c == '\\' && quote == '"')
                {
                    i++;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
            }
            else if (c == '#')
            {
                return line.Substring(0, i);
            }
        }

        return line;
    }
}
