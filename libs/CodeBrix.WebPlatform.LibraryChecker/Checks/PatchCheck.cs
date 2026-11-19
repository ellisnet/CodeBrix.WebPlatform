using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using CodeBrix.WebPlatform.LibraryChecker.Patching;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// Confirms that each build tool's source (in <c>tools/dx-native-tools/sources</c>) is exactly
/// its pristine crates.io archive plus the numbered patches recorded beside the archive (in
/// <c>tools/dx-native-tools/vendoring</c>). A change made directly to the working copy, without a
/// patch, fails this check.
/// </summary>
public static class PatchCheck
{
    /// <summary>
    /// Checks every tool folder of the tool records that holds a <c>.crate</c> archive.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static IReadOnlyList<CheckResult> Run(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var results = new List<CheckResult>();
        if (!Directory.Exists(layout.ToolVendoring))
        {
            return results;
        }

        foreach (var libraryFolder in Directory.EnumerateDirectories(layout.ToolVendoring).OrderBy(d => d, StringComparer.Ordinal))
        {
            var archives = Directory.EnumerateFiles(libraryFolder, "*.crate").OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (archives.Count == 0)
            {
                continue;
            }

            var library = Path.GetFileName(libraryFolder);
            results.Add(CheckLibrary(library, libraryFolder, archives, Path.Combine(layout.ToolSources, library), $"{layout.ToolSourcesRelative}/{library}", $"{layout.ToolVendoringRelative}/{library}"));
        }

        return results;
    }

    /// <summary>
    /// Checks one library: archive checksum, patches apply in order, result equals the working copy.
    /// </summary>
    /// <param name="library">The library's name (its folder name).</param>
    /// <param name="recordFolder">The folder holding the archive, its <c>.sha256</c> and the patches.</param>
    /// <param name="archives">The <c>.crate</c> files found (exactly one is expected).</param>
    /// <param name="workingCopy">The tool's source folder.</param>
    /// <param name="shownWorkingCopy">The source folder as messages name it.</param>
    /// <param name="shownRecordFolder">The record folder as messages name it.</param>
    public static CheckResult CheckLibrary(string library, string recordFolder, IReadOnlyList<string> archives, string workingCopy, string shownWorkingCopy, string shownRecordFolder)
    {
        var name = $"Patches: {shownWorkingCopy} is its crates.io archive plus the recorded patches";
        var problems = new List<string>();
        var notes = new List<string>();
        if (archives.Count != 1)
        {
            return new CheckResult(name, new[] { $"{shownRecordFolder} must hold exactly one .crate archive; it holds {archives.Count}" });
        }

        var archive = archives[0];
        var checksumFile = archive + ".sha256";
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant();
        if (!File.Exists(checksumFile))
        {
            problems.Add($"{Path.GetFileName(checksumFile)} is missing; it must record the archive's crates.io checksum");
        }
        else
        {
            var recorded = File.ReadAllText(checksumFile).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (!string.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{Path.GetFileName(archive)} has checksum {actual} but {Path.GetFileName(checksumFile)} records {recorded}");
            }
        }

        if (!Directory.Exists(workingCopy))
        {
            problems.Add($"{shownWorkingCopy} does not exist");
            return new CheckResult(name, problems);
        }

        var scratch = Path.Combine(Path.GetTempPath(), "webplatform-librarychecker-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pristine = ExtractCrate(archive, scratch);
            var patches = Directory.EnumerateFiles(recordFolder, "*.patch").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
            foreach (var patch in patches)
            {
                try
                {
                    UnifiedDiff.Apply(pristine, File.ReadAllText(patch));
                }
                catch (InvalidDataException exception)
                {
                    problems.Add($"{Path.GetFileName(patch)} does not apply: {exception.Message}");
                    return new CheckResult(name, problems);
                }
            }

            notes.Add(patches.Count == 0 ? "unmodified: no patches" : $"{patches.Count} patch(es): {string.Join(", ", patches.Select(Path.GetFileName))}");
            problems.AddRange(CompareTrees(pristine, workingCopy, shownWorkingCopy));
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }

        return new CheckResult(name, problems, notes);
    }

    /// <summary>
    /// Unpacks a <c>.crate</c> archive (a gzip-compressed tar with one top-level folder) and
    /// returns that folder.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="destination">An empty or new folder to unpack into.</param>
    /// <exception cref="InvalidDataException">The archive holds an entry outside its top-level folder.</exception>
    public static string ExtractCrate(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        string top = null;
        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        TarEntry entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            var relative = entry.Name.Replace('\\', '/');
            var target = Path.GetFullPath(Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The archive entry '{entry.Name}' is outside the archive's folder.");
            }

            top ??= relative.Split('/')[0];
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            entry.ExtractToFile(target, overwrite: true);
        }

        if (top == null)
        {
            throw new InvalidDataException($"The archive '{archive}' holds no files.");
        }

        return Path.Combine(destination, top);
    }

    /// <summary>
    /// Compares two folders byte for byte. The working copy's <c>target</c> folder (cargo's
    /// build output) is not part of the source and is left out.
    /// </summary>
    /// <param name="expected">The pristine archive with the patches applied.</param>
    /// <param name="workingCopy">The tool's source folder.</param>
    /// <param name="shownWorkingCopy">The source folder as the messages name it.</param>
    public static IReadOnlyList<string> CompareTrees(string expected, string workingCopy, string shownWorkingCopy)
    {
        var problems = new List<string>();
        var expectedFiles = ListFiles(expected, skipTarget: false);
        var actualFiles = ListFiles(workingCopy, skipTarget: true);
        foreach (var relative in expectedFiles.Keys.Except(actualFiles.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            problems.Add($"{shownWorkingCopy}/{relative} is missing (the archive plus patches has it)");
        }

        foreach (var relative in actualFiles.Keys.Except(expectedFiles.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            problems.Add($"{shownWorkingCopy}/{relative} is not in the archive and no patch adds it");
        }

        foreach (var relative in expectedFiles.Keys.Intersect(actualFiles.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!File.ReadAllBytes(expectedFiles[relative]).AsSpan().SequenceEqual(File.ReadAllBytes(actualFiles[relative])))
            {
                problems.Add($"{shownWorkingCopy}/{relative} differs from the archive plus patches (a change was made without a patch)");
            }
        }

        return problems;
    }

    private static Dictionary<string, string> ListFiles(string root, bool skipTarget)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (skipTarget && (relative.StartsWith("target/", StringComparison.Ordinal) || relative == ".cargo-lock"))
            {
                continue;
            }

            result[relative] = file;
        }

        return result;
    }
}
