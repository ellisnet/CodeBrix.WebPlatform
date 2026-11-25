using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>
/// Fills the built frontend folder (<see cref="BuiltFrontend.FolderName"/> in the backend
/// project's folder) with the built frontend applications: the root application's site at the
/// folder's top, every named application in the sub-folder of its name, the frontend manifest
/// beside them, and the folder's own <c>.gitignore</c>. The folder belongs to the build
/// entirely; the developer's <c>wwwroot</c> is never touched.
/// </summary>
internal static class BuiltFrontendWriter
{
    /// <summary>The built frontend folder of a backend project folder.</summary>
    /// <param name="backendProjectDirectory">The backend project's folder (its content root).</param>
    /// <returns>The full path of <see cref="BuiltFrontend.FolderName"/> in it.</returns>
    internal static string GetDirectory(string backendProjectDirectory)
        => Path.Combine(Path.GetFullPath(backendProjectDirectory), BuiltFrontend.FolderName);

    /// <summary>
    /// A full build: makes the folder hold exactly the built applications (everything else in it
    /// is removed, except its <c>.gitignore</c>), and writes the manifest for them.
    /// </summary>
    /// <param name="directory">The built frontend folder, as a full path; created when missing.</param>
    /// <param name="sites">Each application with the folder dx built its site into.</param>
    /// <returns>The number of files copied for each application, in order.</returns>
    /// <exception cref="FrontendBuildException">The root application's site has an entry with
    /// the name of another application.</exception>
    internal static IReadOnlyList<int> Write(string directory, IReadOnlyList<(FrontendApplication Application, string Site)> sites)
    {
        var applications = new FrontendApplicationSet(sites.Select(s => s.Application));
        var root = sites.FirstOrDefault(s => s.Application.IsDefault);
        if (root.Site != null)
        {
            RequireNoClash(root.Site, applications);
        }

        Directory.CreateDirectory(directory);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (!BuildOutputFolder.IsIgnoreFile(entry))
            {
                Delete(entry);
            }
        }

        var counts = new List<int>();
        foreach (var (application, site) in sites)
        {
            counts.Add(Copy(site, application.IsDefault ? directory : Path.Combine(directory, application.Folder)));
        }

        FrontendManifestWriter.Write(directory, applications);
        BuildOutputFolder.EnsureIgnoreFile(directory);
        return counts;
    }

    /// <summary>
    /// A build of one application: replaces only that application's part of the folder - a named
    /// application's sub-folder; for the root application, the entries at the folder's top that
    /// are not a named application's sub-folder, the manifest or the <c>.gitignore</c> - and
    /// leaves the other applications' output as it is (or missing). The manifest is written for
    /// every registered application.
    /// </summary>
    /// <param name="directory">The built frontend folder, as a full path; created when missing.</param>
    /// <param name="applications">Every registered application.</param>
    /// <param name="application">The application that was built.</param>
    /// <param name="site">The folder dx built its site into.</param>
    /// <returns>The number of files copied.</returns>
    /// <exception cref="FrontendBuildException">The root application's site has an entry with
    /// the name of another application.</exception>
    internal static int WriteOne(string directory, FrontendApplicationSet applications, FrontendApplication application, string site)
    {
        if (application.IsDefault)
        {
            RequireNoClash(site, applications);
        }

        Directory.CreateDirectory(directory);
        int count;
        if (application.IsDefault)
        {
            var named = new HashSet<string>(applications.Named.Select(a => a.Folder), StringComparer.Ordinal);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var name = Path.GetFileName(entry);
                if (BuildOutputFolder.IsIgnoreFile(entry) || name == FrontendManifest.FileName || (named.Contains(name) && Directory.Exists(entry)))
                {
                    continue;
                }

                Delete(entry);
            }

            count = Copy(site, directory);
        }
        else
        {
            var folder = Path.Combine(directory, application.Folder);
            Delete(folder);
            count = Copy(site, folder);
        }

        FrontendManifestWriter.Write(directory, applications);
        BuildOutputFolder.EnsureIgnoreFile(directory);
        return count;
    }

    private static void RequireNoClash(string rootSite, FrontendApplicationSet applications)
    {
        foreach (var named in applications.Named)
        {
            if (File.Exists(Path.Combine(rootSite, named.Folder)) || Directory.Exists(Path.Combine(rootSite, named.Folder)))
            {
                throw new FrontendBuildException(
                    $"The site of the root application has an entry named '{named.Folder}', which is also the name of an application. Rename that application or the entry.");
            }
        }
    }

    /// <summary>
    /// Copies a site into its place. A <c>.gitignore</c> at the top of the site is not copied:
    /// the folder's own is the build's.
    /// </summary>
    private static int Copy(string site, string target)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(site, file);
            if (relative == BuildOutputFolder.IgnoreFileName)
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
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
