using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;

namespace CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

/// <summary>
/// The shared Rust crate that every platform frontend compiles in: the REST transport, the
/// SignalR client (with a fake hub for native unit tests), the error type that reads
/// CodeBrix.WebPlatform.Core's <c>ApiProblem</c>, query strings, the browser helpers and the
/// calendar date type (in the browser-free types crate inside its folder,
/// <see cref="TypesCrateName"/>). The crate's source travels inside this assembly;
/// <see cref="PlatformFrontend.MaterializeAsync"/> writes it out with
/// <see cref="ExtractTo(string)"/>, and the frontend names the folder as a path dependency in its
/// <c>Cargo.toml</c>.
/// </summary>
public static class SharedRustCrate
{
    /// <summary>The crate's package name, as written in <c>Cargo.toml</c>.</summary>
    public const string CrateName = "codebrix-webplatform";

    /// <summary>The crate's name in Rust source (<c>use codebrix_webplatform::...</c>).</summary>
    public const string RustName = "codebrix_webplatform";

    /// <summary>
    /// The package name of the shared crate's browser-free part, the types the generated client
    /// code names besides serde (the calendar date): a crate of its own, depending on serde only,
    /// in the folder <see cref="TypesFolderName"/> inside the shared crate's folder. The shared
    /// crate re-exports it (<c>codebrix_webplatform::dates</c>), so a frontend never names it; a
    /// native program or test that uses the generated contract types without the browser-bound
    /// crate names it under <see cref="CrateName"/> (<c>package = "codebrix-webplatform-types"</c>).
    /// </summary>
    public const string TypesCrateName = "codebrix-webplatform-types";

    /// <summary>The types crate's name in Rust source.</summary>
    public const string TypesRustName = "codebrix_webplatform_types";

    /// <summary>The folder of the types crate inside the shared crate's folder (<c>platform/codebrix-webplatform/types</c>).</summary>
    public const string TypesFolderName = "types";

    private const string ResourcePrefix = "CodeBrix.WebPlatform.FrontendBuilder.Rust/";
    private const string ManifestFile = "Cargo.toml";

    private static readonly Lazy<IReadOnlyList<SharedRustFile>> LoadedFiles = new(Load);

    /// <summary>Every file of the crate, ordered by path.</summary>
    public static IReadOnlyList<SharedRustFile> Files => LoadedFiles.Value;

    /// <summary>
    /// Makes a folder hold exactly the shared crate. Files that already have the right content
    /// are left untouched (so cargo does not rebuild for nothing), changed and missing files are
    /// written, and files that are not part of the crate are deleted, except cargo's own
    /// <c>target</c> folder and <c>Cargo.lock</c>. The folder is build output: its own
    /// <c>.gitignore</c> (the rule <c>*</c>, under a header saying what wrote it) is written
    /// whenever it is missing or differs, so that git ignores the folder without a rule of the
    /// developer's; it is not listed in the result.
    /// </summary>
    /// <param name="directory">The folder to write the crate into. It is created when missing.
    /// The folder belongs to the crate: never point this at a folder that holds anything else.</param>
    /// <returns>The paths, relative to <paramref name="directory"/> with <c>/</c> separators, of
    /// the files that were written or deleted. Empty when the folder was already up to date.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is null or blank.</exception>
    /// <exception cref="InvalidOperationException">The folder exists, is not empty and is not a
    /// copy of this crate, so writing into it could destroy something else.</exception>
    public static IReadOnlyList<string> ExtractTo(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(e => !BuildOutputFolder.IsIgnoreFile(e)) && !IsCrateFolder(root))
        {
            throw new InvalidOperationException(
                $"The folder '{root}' is not empty and does not hold the {CrateName} crate. Nothing was written. Choose a folder that is used for nothing else.");
        }

        Directory.CreateDirectory(root);
        var changed = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Files)
        {
            if (file.RelativePath == BuildOutputFolder.IgnoreFileName)
            {
                continue;
            }

            expected.Add(file.RelativePath);
            var target = Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(file.Content))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, file.Content);
            changed.Add(file.RelativePath);
        }

        foreach (var existing in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList())
        {
            var relative = Path.GetRelativePath(root, existing).Replace(Path.DirectorySeparatorChar, '/');
            if (expected.Contains(relative) || IsCargoOutput(relative) || relative == BuildOutputFolder.IgnoreFileName)
            {
                continue;
            }

            File.Delete(existing);
            changed.Add(relative);
        }

        BuildOutputFolder.EnsureIgnoreFile(root);
        changed.Sort(StringComparer.Ordinal);
        return changed;
    }

    private static bool IsCargoOutput(string relativePath)
        => relativePath == "Cargo.lock" || relativePath.StartsWith("target/", StringComparison.Ordinal);

    private static bool IsCrateFolder(string root)
    {
        var manifest = Path.Combine(root, ManifestFile);
        if (!File.Exists(manifest))
        {
            return false;
        }

        return File.ReadLines(manifest).Any(line =>
        {
            var compact = line.Replace(" ", string.Empty, StringComparison.Ordinal);
            return compact == $"name=\"{CrateName}\"";
        });
    }

    private static IReadOnlyList<SharedRustFile> Load()
    {
        var assembly = typeof(SharedRustCrate).GetTypeInfo().Assembly;
        var files = new List<SharedRustFile>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files.Add(new SharedRustFile(name.Substring(ResourcePrefix.Length).Replace('\\', '/'), buffer.ToArray()));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return files;
    }
}
