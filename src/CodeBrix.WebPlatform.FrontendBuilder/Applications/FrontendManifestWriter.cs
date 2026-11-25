using System;
using System.IO;
using System.Text;
using CodeBrix.WebPlatform.Core.Applications;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>
/// Writes the frontend manifest (<see cref="FrontendManifest"/>): the small file in the built
/// frontend folder (<see cref="BuiltFrontend.FolderName"/>) that tells the backend which
/// frontend applications are registered and where their sites are. The frontend build writes
/// it whenever it fills that folder, for every registered application; the backend reads it
/// when it starts. The file is build output.
/// </summary>
public static class FrontendManifestWriter
{
    /// <summary>The text of the manifest for a set of applications (UTF-8, line feeds).</summary>
    /// <param name="applications">The registered applications.</param>
    /// <returns>The file's content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="applications"/> is null.</exception>
    public static string Create(FrontendApplicationSet applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        var text = new StringBuilder();
        text.Append("# Written by CodeBrix.WebPlatform.FrontendBuilder when the frontend is built: the frontend\n");
        text.Append("# applications in this folder, for the backend that serves them.\n");
        text.Append("# Build output: never edit it and do not commit it; every build writes it anew.\n");
        text.Append(FrontendManifest.WrittenByKey).Append(" = \"").Append(FrontendManifest.WrittenBy).Append("\"\n");
        text.Append(FrontendManifest.FormatKey).Append(" = ").Append(FrontendManifest.CurrentFormat).Append('\n');
        foreach (var application in applications)
        {
            // Names and folders are lowercase letters, digits and hyphens, so they need no escaping.
            text.Append('\n');
            text.Append("[[").Append(FrontendManifest.ApplicationTable).Append("]]\n");
            text.Append(FrontendManifest.NameKey).Append(" = \"").Append(application.Name).Append("\"\n");
            text.Append(FrontendManifest.DefaultKey).Append(" = ").Append(application.IsDefault ? "true" : "false").Append('\n');
            text.Append(FrontendManifest.FolderKey).Append(" = \"").Append(application.Folder).Append("\"\n");
        }

        return text.ToString();
    }

    /// <summary>The manifest's path in a built frontend folder.</summary>
    /// <param name="builtFrontendDirectory">The built frontend folder.</param>
    /// <returns>The path of <see cref="FrontendManifest.FileName"/> in it.</returns>
    public static string GetPath(string builtFrontendDirectory)
        => Path.Combine(Path.GetFullPath(builtFrontendDirectory), FrontendManifest.FileName);

    /// <summary>Writes the manifest into a built frontend folder, replacing the one that is there.</summary>
    /// <param name="builtFrontendDirectory">The built frontend folder; created when missing.</param>
    /// <param name="applications">The registered applications.</param>
    /// <returns>The path of the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="applications"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="builtFrontendDirectory"/> is blank.</exception>
    public static string Write(string builtFrontendDirectory, FrontendApplicationSet applications)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builtFrontendDirectory);
        var path = GetPath(builtFrontendDirectory);
        var text = Create(applications);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }
}
