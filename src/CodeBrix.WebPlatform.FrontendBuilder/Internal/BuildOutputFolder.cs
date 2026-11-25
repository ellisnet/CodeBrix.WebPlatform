using System;
using System.IO;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// The <c>.gitignore</c> file this library keeps in every folder it owns (the vendor folder, the
/// shared crate, <c>.cargo</c>, <c>target</c>, <c>assets/generated</c> and the backend's
/// <c>frontend-built</c>), so that git ignores the folder's content - the file included -
/// without an ignore rule of the developer's. It is written again whenever it is missing or
/// differs, on every materialize and build. The library never writes a <c>.gitignore</c> in a
/// folder the developer owns.
/// </summary>
internal static class BuildOutputFolder
{
    /// <summary>The ignore file's name.</summary>
    internal const string IgnoreFileName = ".gitignore";

    /// <summary>The first line of the ignore file.</summary>
    internal const string IgnoreFileMarker = "# Written by CodeBrix.WebPlatform.FrontendBuilder: this folder is build output.";

    /// <summary>The ignore file's content: the marker header, then the one rule <c>*</c>.</summary>
    internal const string IgnoreFileText = IgnoreFileMarker + "\n"
        + "# Git ignores everything in it, this file included. Never edit or commit what is here; the\n"
        + "# build writes this file again when it is missing.\n"
        + "*\n";

    /// <summary>
    /// Makes a folder hold the ignore file with <see cref="IgnoreFileText"/>; the folder is
    /// created when missing. Nothing is written when the file already has that content.
    /// </summary>
    /// <param name="directory">The folder, which belongs to the build.</param>
    /// <returns>True when the file was written.</returns>
    internal static bool EnsureIgnoreFile(string directory)
    {
        var path = Path.Combine(directory, IgnoreFileName);
        if (File.Exists(path) && string.Equals(File.ReadAllText(path, Encoding.UTF8), IgnoreFileText, StringComparison.Ordinal))
        {
            return false;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, IgnoreFileText, new UTF8Encoding(false));
        return true;
    }

    /// <summary>True when the path names a file called <see cref="IgnoreFileName"/>.</summary>
    /// <param name="path">A path or a file name.</param>
    internal static bool IsIgnoreFile(string path)
        => string.Equals(Path.GetFileName(path), IgnoreFileName, StringComparison.Ordinal);
}
