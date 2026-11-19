using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

/// <summary>
/// The shared Rust crate that every platform frontend compiles in: the REST transport, the
/// SignalR client (with a fake hub for native unit tests), the error type that reads
/// CodeBrix.WebPlatform.Core's <c>ApiProblem</c>, query-string building and the calendar date
/// type. The crate's source travels inside this assembly;
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

    private const string ResourcePrefix = "CodeBrix.WebPlatform.FrontendBuilder.Rust/";
    private const string ManifestFile = "Cargo.toml";

    private static readonly Lazy<IReadOnlyList<SharedRustFile>> LoadedFiles = new(Load);

    /// <summary>Every file of the crate, ordered by path.</summary>
    public static IReadOnlyList<SharedRustFile> Files => LoadedFiles.Value;

    /// <summary>
    /// Makes a folder hold exactly the shared crate. Files that already have the right content
    /// are left untouched (so cargo does not rebuild for nothing), changed and missing files are
    /// written, and files that are not part of the crate are deleted, except cargo's own
    /// <c>target</c> folder and <c>Cargo.lock</c>.
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
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !IsCrateFolder(root))
        {
            throw new InvalidOperationException(
                $"The folder '{root}' is not empty and does not hold the {CrateName} crate. Nothing was written. Choose a folder that is used for nothing else.");
        }

        Directory.CreateDirectory(root);
        var changed = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Files)
        {
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
            if (expected.Contains(relative) || IsCargoOutput(relative))
            {
                continue;
            }

            File.Delete(existing);
            changed.Add(relative);
        }

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
