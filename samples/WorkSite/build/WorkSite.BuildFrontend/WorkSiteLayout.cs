using System;
using System.IO;

namespace WorkSite.BuildFrontend;

/// <summary>
/// The folders and files of the WorkSite sample, all derived from its root folder (the one that
/// holds WorkSite.slnx).
/// </summary>
public sealed class WorkSiteLayout
{
    /// <summary>The file, relative to the sample's root folder, that marks it.</summary>
    public const string MarkerFile = "WorkSite.slnx";

    /// <summary>
    /// Creates a layout for a known root folder.
    /// </summary>
    /// <param name="root">The WorkSite sample folder.</param>
    public WorkSiteLayout(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The WorkSite sample folder.</summary>
    public string Root { get; }

    /// <summary>The repository root (two levels above the sample folder).</summary>
    public string RepositoryRoot => Path.GetFullPath(Path.Combine(Root, "..", ".."));

    /// <summary>The Dioxus frontend crate.</summary>
    public string Frontend => Path.Combine(Root, "frontend");

    /// <summary>The backend folder.</summary>
    public string Backend => Path.Combine(Root, "backend");

    /// <summary>
    /// WorkSite's own third-party notices (hand-written: what is WorkSite's beyond the platform).
    /// The site serves it followed by the platform's notices.
    /// </summary>
    public string NoticesFile => Path.Combine(Root, "THIRD-PARTY-NOTICES.txt");

    /// <summary>
    /// True when a folder is the WorkSite sample folder.
    /// </summary>
    /// <param name="directory">The folder.</param>
    public static bool IsRoot(string directory)
        => !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, MarkerFile));

    /// <summary>
    /// Finds the WorkSite sample folder by walking up from <paramref name="start"/> until a
    /// folder holds the marker file, also looking in <c>samples/WorkSite</c> below each folder on
    /// the way (so the program works from the repository root too).
    /// </summary>
    /// <param name="start">Where to start; a file path is reduced to its folder.</param>
    /// <returns>The layout, or <c>null</c> when no WorkSite folder was found.</returns>
    public static WorkSiteLayout Find(string start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory != null; directory = directory.Parent)
        {
            if (IsRoot(directory.FullName))
            {
                return new WorkSiteLayout(directory.FullName);
            }

            var nested = Path.Combine(directory.FullName, "samples", "WorkSite");
            if (IsRoot(nested))
            {
                return new WorkSiteLayout(nested);
            }
        }

        return null;
    }
}
