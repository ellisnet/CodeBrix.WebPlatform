using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WorkSite.BuildFrontend;

/// <summary>
/// Reads and creates the <c>.env</c> file that holds the containers' settings and secrets.
/// </summary>
public static class EnvFile
{
    /// <summary>The settings that must have a value before the containers can start.</summary>
    public static readonly IReadOnlyList<string> RequiredKeys = new[]
    {
        "WORKSITE_DB_PASSWORD", "WORKSITE_JWT_SIGNING_KEY", "WORKSITE_CERT_FILE", "WORKSITE_CERT_PASSWORD",
    };

    /// <summary>
    /// Reads <c>KEY=value</c> lines; blank lines and lines starting with <c>#</c> are skipped,
    /// and a value in matching single or double quotes is unquoted.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length == 0 || line[0] == '#' || equals <= 0)
            {
                continue;
            }

            var value = line.Substring(equals + 1).Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value.Substring(1, value.Length - 2);
            }

            result[line.Substring(0, equals).Trim()] = value;
        }

        return result;
    }

    /// <summary>
    /// Reads the file; an absent file gives an empty set.
    /// </summary>
    /// <param name="path">The <c>.env</c> file.</param>
    public static IReadOnlyDictionary<string, string> Load(string path)
        => File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>();

    /// <summary>
    /// Returns the required settings that are missing or empty.
    /// </summary>
    /// <param name="values">The settings read from the file.</param>
    public static IReadOnlyList<string> MissingKeys(IReadOnlyDictionary<string, string> values)
        => RequiredKeys.Where(k => !values.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();

    /// <summary>
    /// Builds the text of a new <c>.env</c> from the example file: every empty secret gets a
    /// random value, the certificate file gets <paramref name="certificateFile"/>, and every
    /// other line is kept as it is.
    /// </summary>
    /// <param name="exampleText">The text of <c>.env.example</c>.</param>
    /// <param name="certificateFile">The exported certificate's full path.</param>
    public static string CreateFromExample(string exampleText, string certificateFile)
    {
        var builder = new StringBuilder();
        foreach (var raw in (exampleText ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length > 0 && line[0] != '#' && equals > 0 && line.Substring(equals + 1).Trim().Length == 0)
            {
                var key = line.Substring(0, equals).Trim();
                var value = key switch
                {
                    "WORKSITE_CERT_FILE" => certificateFile,
                    "WORKSITE_JWT_SIGNING_KEY" => RandomSecret(48),
                    "WORKSITE_DEMO_PASSWORD" => RandomSecret(12),
                    _ when key.EndsWith("_PASSWORD", StringComparison.Ordinal) => RandomSecret(24),
                    _ => string.Empty,
                };
                line = key + "=" + value;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Creates a random secret of letters and digits (no characters that need quoting in a
    /// <c>.env</c> file, a connection string or a shell).
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static string RandomSecret(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }
}
