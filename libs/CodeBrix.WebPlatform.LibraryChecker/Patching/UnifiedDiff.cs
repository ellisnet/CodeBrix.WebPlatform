using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CodeBrix.WebPlatform.LibraryChecker.Patching;

/// <summary>
/// Applies a unified diff (as written by <c>diff -ruN</c>) to a folder, strictly: every hunk
/// must match the file exactly at the line the hunk names. There is no fuzz and no offset
/// search, so a patch either applies to exactly the source it was written against or fails.
/// A file that the patch creates has an old range of <c>-0,0</c>; a file it removes ends up
/// with no content and is deleted.
/// <para>
/// One form goes beyond what <c>diff</c> writes, for removing a file that a text diff cannot
/// carry (a binary): a line <c>delete-file sha256:&lt;hex&gt; a/&lt;path&gt;</c>, on its own
/// outside any hunk. It is as strict as a hunk: the file must exist and its SHA-256 must equal
/// the one named, otherwise the patch fails.
/// </para>
/// </summary>
public static class UnifiedDiff
{
    /// <summary>
    /// Applies a patch to a folder.
    /// </summary>
    /// <param name="root">The folder the patch's paths are relative to.</param>
    /// <param name="patchText">The patch text.</param>
    /// <returns>The relative paths (forward slashes) of the files the patch changed, created or
    /// removed, in patch order.</returns>
    /// <exception cref="InvalidDataException">The patch is malformed or does not apply.</exception>
    public static IReadOnlyList<string> Apply(string root, string patchText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(patchText);
        var touched = new List<string>();
        foreach (var file in Parse(patchText))
        {
            var path = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var exists = File.Exists(path);
            if (file.DeleteSha256 != null)
            {
                if (!exists)
                {
                    throw new InvalidDataException($"The patch deletes '{file.Path}' but the file does not exist.");
                }

                var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                if (!string.Equals(actual, file.DeleteSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The patch deletes '{file.Path}' but the file is not the one the patch names (its SHA-256 is {actual.ToLowerInvariant()}).");
                }

                File.Delete(path);
                touched.Add(file.Path);
                continue;
            }

            var creates = file.Hunks.Count == 1 && file.Hunks[0].OldStart == 0 && file.Hunks[0].OldCount == 0;
            if (creates && exists)
            {
                throw new InvalidDataException($"The patch creates '{file.Path}' but the file already exists.");
            }

            if (!creates && !exists)
            {
                throw new InvalidDataException($"The patch changes '{file.Path}' but the file does not exist.");
            }

            var originalEndsWithNewline = true;
            var original = exists ? SplitLines(File.ReadAllText(path, new UTF8Encoding(false)), out originalEndsWithNewline) : new List<string>();

            var result = new List<string>();
            var position = 0;
            var endsWithNewline = originalEndsWithNewline;
            foreach (var hunk in file.Hunks)
            {
                var start = hunk.OldCount == 0 ? hunk.OldStart : hunk.OldStart - 1;
                if (start < position || start > original.Count)
                {
                    throw new InvalidDataException($"A hunk of '{file.Path}' starts at line {hunk.OldStart}, which is out of order or beyond the end of the file.");
                }

                result.AddRange(original.Skip(position).Take(start - position));
                position = start;
                foreach (var line in hunk.Lines)
                {
                    if (line.Kind == ' ' || line.Kind == '-')
                    {
                        if (position >= original.Count || !string.Equals(original[position], line.Text, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException($"A hunk of '{file.Path}' does not match the file at line {position + 1}.");
                        }

                        if (line.Kind == ' ')
                        {
                            result.Add(line.Text);
                        }

                        position++;
                    }
                    else
                    {
                        result.Add(line.Text);
                    }
                }

                if (hunk.NewEndsWithoutNewline.HasValue && position == original.Count)
                {
                    endsWithNewline = !hunk.NewEndsWithoutNewline.Value;
                }
            }

            result.AddRange(original.Skip(position));
            touched.Add(file.Path);
            if (result.Count == 0 && !creates)
            {
                File.Delete(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Join("\n", result) + (endsWithNewline && result.Count > 0 ? "\n" : string.Empty), new UTF8Encoding(false));
        }

        return touched;
    }

    /// <summary>
    /// Returns the relative paths (forward slashes) a patch names, without applying it.
    /// </summary>
    /// <param name="patchText">The patch text.</param>
    /// <exception cref="InvalidDataException">The patch is malformed.</exception>
    public static IReadOnlyList<string> ListFiles(string patchText)
    {
        ArgumentNullException.ThrowIfNull(patchText);
        return Parse(patchText).Select(f => f.Path).ToList();
    }

    private const string DeleteDirective = "delete-file ";

    private static List<PatchedFile> Parse(string patchText)
    {
        var lines = patchText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var files = new List<PatchedFile>();
        PatchedFile file = null;
        Hunk hunk = null;
        var oldRemaining = 0;
        var newRemaining = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (hunk != null && (oldRemaining > 0 || newRemaining > 0))
            {
                if (line.Length == 0)
                {
                    // An empty context line whose leading space was trimmed.
                    line = " ";
                }

                var kind = line[0];
                if (kind == '\\')
                {
                    continue;
                }

                if (kind != ' ' && kind != '+' && kind != '-')
                {
                    throw new InvalidDataException($"Unexpected line {i + 1} inside a hunk of '{file.Path}'.");
                }

                hunk.Lines.Add(new HunkLine { Kind = kind, Text = line.Substring(1) });
                if (kind != '+')
                {
                    oldRemaining--;
                }

                if (kind != '-')
                {
                    newRemaining--;
                }

                if (oldRemaining == 0 && newRemaining == 0 && i + 1 < lines.Length && lines[i + 1].StartsWith("\\ ", StringComparison.Ordinal))
                {
                    // "\ No newline at end of file" after the hunk's last line: it applies to
                    // the new file when that last line is kept or added.
                    hunk.NewEndsWithoutNewline = kind != '-';
                    i++;
                }
                else if (kind != '-' && oldRemaining == 0 && newRemaining == 0)
                {
                    hunk.NewEndsWithoutNewline ??= false;
                }

                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal) && i + 1 < lines.Length && lines[i + 1].StartsWith("+++ ", StringComparison.Ordinal))
            {
                file = new PatchedFile { Path = StripPrefix(lines[i + 1].Substring(4)) };
                files.Add(file);
                hunk = null;
                i++;
                continue;
            }

            if (line.StartsWith(DeleteDirective, StringComparison.Ordinal))
            {
                files.Add(ParseDelete(line, i + 1));
                file = null;
                hunk = null;
                continue;
            }

            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                if (file == null)
                {
                    throw new InvalidDataException($"Line {i + 1}: a hunk without a file header.");
                }

                hunk = ParseHunkHeader(line, i + 1);
                file.Hunks.Add(hunk);
                oldRemaining = hunk.OldCount;
                newRemaining = hunk.NewCount;
            }
        }

        if (oldRemaining > 0 || newRemaining > 0)
        {
            throw new InvalidDataException("The patch ends in the middle of a hunk.");
        }

        if (files.Any(f => f.Hunks.Count == 0 && f.DeleteSha256 == null))
        {
            throw new InvalidDataException("The patch names a file without any hunk.");
        }

        return files;
    }

    private static PatchedFile ParseDelete(string line, int lineNumber)
    {
        // delete-file sha256:<64 hex digits> a/<path>
        var rest = line.Substring(DeleteDirective.Length);
        var space = rest.IndexOf(' ', StringComparison.Ordinal);
        const string Prefix = "sha256:";
        if (space < 0 || !rest.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Line {lineNumber}: malformed delete-file line; expected 'delete-file sha256:<hex> a/<path>'.");
        }

        var hash = rest.Substring(Prefix.Length, space - Prefix.Length);
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"Line {lineNumber}: the delete-file line does not name a SHA-256 (64 hexadecimal digits).");
        }

        return new PatchedFile { Path = StripPrefix(rest.Substring(space + 1)), DeleteSha256 = hash };
    }

    private static Hunk ParseHunkHeader(string line, int lineNumber)
    {
        // @@ -oldStart[,oldCount] +newStart[,newCount] @@
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts[1].Length < 2 || parts[1][0] != '-' || parts[2].Length < 2 || parts[2][0] != '+')
        {
            throw new InvalidDataException($"Line {lineNumber}: malformed hunk header.");
        }

        var (oldStart, oldCount) = ParseRange(parts[1].Substring(1), lineNumber);
        var (_, newCount) = ParseRange(parts[2].Substring(1), lineNumber);
        return new Hunk { OldStart = oldStart, OldCount = oldCount, NewCount = newCount };
    }

    private static (int Start, int Count) ParseRange(string text, int lineNumber)
    {
        var comma = text.IndexOf(',', StringComparison.Ordinal);
        var startText = comma < 0 ? text : text.Substring(0, comma);
        var countText = comma < 0 ? "1" : text.Substring(comma + 1);
        if (!int.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            throw new InvalidDataException($"Line {lineNumber}: malformed hunk range '{text}'.");
        }

        return (start, count);
    }

    private static string StripPrefix(string header)
    {
        // "b/src/main.rs<TAB>timestamp" -> "src/main.rs"
        var tab = header.IndexOf('\t', StringComparison.Ordinal);
        var path = (tab < 0 ? header : header.Substring(0, tab)).Trim();
        var slash = path.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || slash == path.Length - 1)
        {
            throw new InvalidDataException($"The patch path '{path}' has no leading folder to strip.");
        }

        return path.Substring(slash + 1);
    }

    private static List<string> SplitLines(string text, out bool endsWithNewline)
    {
        endsWithNewline = text.EndsWith('\n');
        if (text.Length == 0)
        {
            return new List<string>();
        }

        var body = endsWithNewline ? text.Substring(0, text.Length - 1) : text;
        return body.Split('\n').ToList();
    }

    private sealed class PatchedFile
    {
        public string Path { get; init; }

        public string DeleteSha256 { get; init; }

        public List<Hunk> Hunks { get; } = new List<Hunk>();
    }

    private sealed class Hunk
    {
        public int OldStart { get; init; }

        public int OldCount { get; init; }

        public int NewCount { get; init; }

        public bool? NewEndsWithoutNewline { get; set; }

        public List<HunkLine> Lines { get; } = new List<HunkLine>();
    }

    private sealed class HunkLine
    {
        public char Kind { get; init; }

        public string Text { get; init; }
    }
}
