using System;
using System.Linq;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Shortens a program's output for an error message.
/// </summary>
internal static class OutputText
{
    /// <summary>
    /// The last <paramref name="lines"/> non-blank lines of <paramref name="text"/>, each indented
    /// and cut at 400 characters.
    /// </summary>
    /// <param name="text">A program's output; <c>null</c> is treated as empty.</param>
    /// <param name="lines">How many lines to keep.</param>
    public static string Tail(string text, int lines)
    {
        var all = (text ?? string.Empty).Split('\n').Where(l => l.Trim().Length > 0).Select(l => l.Length > 400 ? l.Substring(0, 400) + " ..." : l).ToList();
        return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Count - lines)).Select(l => "    " + l));
    }

    /// <summary>
    /// The first non-blank line of <paramref name="text"/>, trimmed; empty when there is none.
    /// </summary>
    /// <param name="text">A program's output; <c>null</c> is treated as empty.</param>
    public static string FirstLine(string text)
        => (text ?? string.Empty).Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
}
