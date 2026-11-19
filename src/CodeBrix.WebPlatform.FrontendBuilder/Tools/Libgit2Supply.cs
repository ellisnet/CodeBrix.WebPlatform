using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>
/// The libgit2 library prepared for one dx: the folder that holds it under the name dx asks the
/// loader for, and the environment variable that folder goes first on. Made by
/// <see cref="Libgit2Supply.Prepare"/>.
/// </summary>
internal sealed class Libgit2Supply
{
    /// <summary>The package that carries the library.</summary>
    public const string PackageId = "CodeBrix.LibGit2.MitLicenseForever";

    private Libgit2Supply(Libgit2Naming naming, string neededName, string sourceFile, string directory, string packageVersion)
    {
        Naming = naming;
        NeededName = neededName;
        SourceFile = sourceFile;
        Directory = directory;
        PackageVersion = packageVersion;
    }

    /// <summary>The names on this kind of machine.</summary>
    public Libgit2Naming Naming { get; }

    /// <summary>The name dx was linked against, for example <c>libgit2-49e408b.so.1.9</c>;
    /// <c>null</c> when dx does not link libgit2.</summary>
    public string NeededName { get; }

    /// <summary>The package's file the library was taken from; <c>null</c> when dx does not need it.</summary>
    public string SourceFile { get; }

    /// <summary>The build-owned folder holding the library under the loader's name; <c>null</c> when dx does not need it.</summary>
    public string Directory { get; }

    /// <summary>The CodeBrix.LibGit2 package's version, as its assembly reports it.</summary>
    public string PackageVersion { get; }

    /// <summary>True when dx links libgit2 (every dx of this platform does).</summary>
    public bool IsNeeded => NeededName != null;

    /// <summary>The file name in <see cref="Directory"/> that the loader opens.</summary>
    public string LoaderFileName => NeededName == null ? null : Libgit2Naming.LoaderFileName(NeededName);

    /// <summary>
    /// Finds the package's library file beside the build program: in
    /// <c>runtimes/&lt;rid&gt;/native/</c> (a build for any runtime) or in the build program's own
    /// folder (a build for one runtime, where NuGet puts native files beside the program).
    /// </summary>
    /// <param name="baseDirectory">The build program's folder.</param>
    /// <param name="runtimeIdentifier">This machine's runtime identifier.</param>
    /// <param name="fileName">The package's file name (<see cref="Libgit2Naming.PackageFileName"/>).</param>
    /// <returns>The candidates in the order they are tried.</returns>
    public static IReadOnlyList<string> CandidateFiles(string baseDirectory, string runtimeIdentifier, string fileName)
        => new[]
        {
            Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native", fileName),
            Path.Combine(baseDirectory, fileName),
        };

    /// <summary>
    /// Prepares libgit2 for a dx: reads which library dx was linked against, finds the package's
    /// file, checks that it is that library, and copies it, under the loader's name, into
    /// <paramref name="supplyDirectory"/> (again only when the package's file changed).
    /// </summary>
    /// <param name="dxPath">The dx program.</param>
    /// <param name="runtimeIdentifier">This machine's runtime identifier.</param>
    /// <param name="baseDirectory">The build program's folder, where the package's native file is.</param>
    /// <param name="supplyDirectory">The build-owned folder to put the library in.</param>
    /// <param name="commit">The libgit2 commit the package names its native file after.</param>
    /// <param name="packageVersion">The package's version, for messages.</param>
    /// <param name="supply">The prepared library; <c>null</c> when it could not be prepared.</param>
    /// <returns><c>null</c> when prepared (or not needed), otherwise a message that says what to do.</returns>
    public static string Prepare(string dxPath, string runtimeIdentifier, string baseDirectory, string supplyDirectory, string commit, string packageVersion, out Libgit2Supply supply)
    {
        supply = null;
        var naming = Libgit2Naming.For(runtimeIdentifier, commit);
        if (naming == null)
        {
            return $"libgit2 cannot be supplied to dx on this machine ('{runtimeIdentifier}').";
        }

        string needed;
        try
        {
            needed = naming.FindLoaderName(File.ReadAllBytes(dxPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"dx at '{dxPath}' could not be read: {exception.Message}";
        }

        if (needed == null)
        {
            supply = new Libgit2Supply(naming, null, null, null, packageVersion);
            return null;
        }

        string source = null;
        foreach (var candidate in CandidateFiles(baseDirectory, runtimeIdentifier, naming.PackageFileName))
        {
            if (File.Exists(candidate))
            {
                source = candidate;
                break;
            }
        }

        if (source == null)
        {
            var dxCommit = Libgit2Naming.CommitOf(needed);
            var hint = dxCommit != null && !string.Equals(dxCommit, commit, StringComparison.OrdinalIgnoreCase)
                ? $" The two also disagree on the libgit2 release: dx was linked against commit {dxCommit}, the {PackageId} package {packageVersion} carries commit {commit}."
                : string.Empty;
            return $"dx does not start without libgit2 ('{needed}'), and the {PackageId} package's {naming.PackageFileName} is not beside the build program "
                + $"(looked for {string.Join(" and ", CandidateFiles(baseDirectory, runtimeIdentifier, naming.PackageFileName))}). "
                + "The CodeBrix.WebPlatform.FrontendBuilder package depends on that package; restore and build the build program again." + hint;
        }

        string provided;
        try
        {
            provided = naming.FindLoaderName(File.ReadAllBytes(source));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"The libgit2 library '{source}' could not be read: {exception.Message}";
        }

        if (!string.Equals(provided, needed, naming.System == Libgit2System.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            var neededCommit = Libgit2Naming.CommitOf(needed);
            var commits = neededCommit != null && !string.Equals(neededCommit, commit, StringComparison.OrdinalIgnoreCase)
                ? $" (libgit2 commit {neededCommit} in dx, commit {commit} in the package)"
                : string.Empty;
            return $"dx and the libgit2 library disagree on the library version: dx ('{dxPath}') was linked against '{needed}', "
                + $"but the {PackageId} package {packageVersion} provides '{provided ?? "a library with no recognisable name"}' ('{source}'){commits}. "
                + "Use a dx and a CodeBrix.LibGit2 package built for the same libgit2 release.";
        }

        var directory = Path.GetFullPath(supplyDirectory);
        try
        {
            CopyWhenChanged(source, Path.Combine(directory, Libgit2Naming.LoaderFileName(needed)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"libgit2 could not be put where dx finds it ('{directory}'): {exception.Message}";
        }

        supply = new Libgit2Supply(naming, needed, source, directory, packageVersion);
        return null;
    }

    /// <summary>
    /// The environment dx runs with, with this library's folder first on the loader's search
    /// variable (<see cref="Libgit2Naming.LoaderVariable"/>): <paramref name="environment"/>'s
    /// own value of that variable when it has one, else the current process's.
    /// </summary>
    /// <param name="environment">The variables dx is started with; changed in place.</param>
    public void ApplyTo(IDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!IsNeeded)
        {
            return;
        }

        var variable = Naming.LoaderVariable;
        var current = environment.TryGetValue(variable, out var value) ? value : Environment.GetEnvironmentVariable(variable);
        environment[variable] = string.IsNullOrEmpty(current) ? Directory : Directory + Path.PathSeparator + current;
    }

    /// <summary>
    /// Copies a file unless the target already has the same length and modification time; the
    /// copy is written beside the target and then moved over it, and keeps the source's time.
    /// </summary>
    /// <param name="source">The file to copy.</param>
    /// <param name="target">Where it goes.</param>
    /// <returns>True when the file was copied.</returns>
    internal static bool CopyWhenChanged(string source, string target)
    {
        var from = new FileInfo(source);
        var to = new FileInfo(target);
        if (to.Exists && to.Length == from.Length && to.LastWriteTimeUtc == from.LastWriteTimeUtc)
        {
            return false;
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target));
        var temporary = target + ".partial";
        File.Copy(source, temporary, overwrite: true);
        File.SetLastWriteTimeUtc(temporary, from.LastWriteTimeUtc);
        File.Move(temporary, target, overwrite: true);
        return true;
    }
}
