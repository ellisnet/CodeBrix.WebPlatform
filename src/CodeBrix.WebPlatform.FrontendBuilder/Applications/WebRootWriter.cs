using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>
/// Fills a web root with the built frontend applications: the default application in the web
/// root itself, every named application in the sub-folder of its name, and the frontend
/// manifest beside them.
/// </summary>
internal static class WebRootWriter
{
    /// <summary>A file that is kept when a web root is emptied.</summary>
    internal const string KeptFileName = ".gitkeep";

    /// <summary>
    /// Replaces what the build owns in a web root with the built sites, and writes the manifest.
    /// With a default application the build owns the whole web root (a <c>.gitkeep</c> file is
    /// kept), as it always has. When every application has a name it owns only the applications'
    /// sub-folders (those of this build and of the build before it) and the manifest: anything
    /// else in the web root - a page of the developer's own, for example - is left alone.
    /// </summary>
    /// <param name="webRoot">The web root, as a full path; created when missing.</param>
    /// <param name="sites">Each application with the folder dx built its site into.</param>
    /// <returns>The number of files copied for each application, in order.</returns>
    /// <exception cref="FrontendBuildException">The default application's site has an entry
    /// with the name of another application.</exception>
    internal static IReadOnlyList<int> Write(string webRoot, IReadOnlyList<(FrontendApplication Application, string Site)> sites)
    {
        var applications = new FrontendApplicationSet(sites.Select(s => s.Application));
        var root = sites.FirstOrDefault(s => s.Application.IsDefault);
        if (root.Site != null)
        {
            foreach (var named in applications.Named)
            {
                if (File.Exists(Path.Combine(root.Site, named.Folder)) || Directory.Exists(Path.Combine(root.Site, named.Folder)))
                {
                    throw new FrontendBuildException(
                        $"The site of the application at the site root has an entry named '{named.Folder}', which is also the name of an application. Rename that application or the entry.");
                }
            }
        }

        Directory.CreateDirectory(webRoot);
        if (root.Site != null)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(webRoot))
            {
                if (Path.GetFileName(entry) != KeptFileName)
                {
                    Delete(entry);
                }
            }
        }
        else
        {
            var owned = new HashSet<string>(ReadFolders(Path.Combine(webRoot, FrontendManifest.FileName)), StringComparer.Ordinal);
            owned.UnionWith(applications.Named.Select(a => a.Folder));
            foreach (var folder in owned)
            {
                Delete(Path.Combine(webRoot, folder));
            }

            Delete(Path.Combine(webRoot, FrontendManifest.FileName));
        }

        var counts = new List<int>();
        foreach (var (application, site) in sites)
        {
            counts.Add(Copy(site, application.IsDefault ? webRoot : Path.Combine(webRoot, application.Folder)));
        }

        FrontendManifestWriter.Write(webRoot, applications);
        return counts;
    }

    /// <summary>
    /// The sub-folders a manifest names, read leniently (the file may be from an older build):
    /// only values that are valid application names are returned, so nothing else can ever be
    /// deleted on the word of that file.
    /// </summary>
    internal static IReadOnlyList<string> ReadFolders(string manifestFile)
    {
        var folders = new List<string>();
        if (!File.Exists(manifestFile))
        {
            return folders;
        }

        var prefix = FrontendManifest.FolderKey + " = \"";
        foreach (var raw in File.ReadAllLines(manifestFile))
        {
            var line = raw.Trim();
            if (line.StartsWith(prefix, StringComparison.Ordinal) && line.EndsWith('"') && line.Length > prefix.Length)
            {
                var folder = line.Substring(prefix.Length, line.Length - prefix.Length - 1);
                if (FrontendApplication.DescribeNameProblem(folder) == null)
                {
                    folders.Add(folder);
                }
            }
        }

        return folders;
    }

    private static int Copy(string site, string target)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(site, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination, overwrite: true);
            count++;
        }

        return count;
    }

    private static void Delete(string entry)
    {
        if (Directory.Exists(entry))
        {
            Directory.Delete(entry, recursive: true);
        }
        else if (File.Exists(entry))
        {
            File.Delete(entry);
        }
    }
}
