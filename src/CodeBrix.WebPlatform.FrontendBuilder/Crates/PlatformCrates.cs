using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// The platform's crates: every crate a platform frontend may use without adding crates of its
/// own, exactly as crates.io published them, and the third-party notices for them. The package
/// carries them as ONE archive, which it copies beside the build program as
/// <c>&lt;build program folder&gt;/frontend-crates/platform-crates.zip</c>, with
/// <c>THIRD-PARTY-NOTICES-PlatformCrates.txt</c> beside it.
/// <see cref="PlatformFrontend.MaterializeAsync"/> writes the crates into the frontend as a vendor
/// folder (see <see cref="CrateMode"/>).
/// </summary>
public static class PlatformCrates
{
    /// <summary>The folder, beside the build program, that holds the archive and the notices.</summary>
    public const string FolderName = "frontend-crates";

    /// <summary>The file name of the archive of the crates.</summary>
    public const string ArchiveFileName = "platform-crates.zip";

    /// <summary>
    /// The file name of the generated third-party notices for the platform's crates and the
    /// shared crate's own third-party code. Everything the platform compiles into an
    /// application's WebAssembly file is recorded in it; a built site should carry it.
    /// </summary>
    public const string NoticesFileName = "THIRD-PARTY-NOTICES-PlatformCrates.txt";

    /// <summary>
    /// The file, inside a materialized vendor folder, that records which archive it was written
    /// from (its SHA-256). cargo ignores files whose names start with a dot.
    /// </summary>
    public const string StampFileName = ".codebrix-platform-crates";

    private const string LockResourceName = "CodeBrix.WebPlatform.FrontendBuilder.PlatformCrates/Cargo.lock";

    /// <summary>
    /// The folder that holds the archive and the notices below an application's base folder.
    /// </summary>
    /// <param name="applicationBaseDirectory">The build program's base folder; <c>null</c> means
    /// <see cref="AppContext.BaseDirectory"/>.</param>
    public static string GetDirectory(string applicationBaseDirectory = null)
        => Path.Combine(string.IsNullOrWhiteSpace(applicationBaseDirectory) ? AppContext.BaseDirectory : applicationBaseDirectory, FolderName);

    /// <summary>The archive of the crates below an application's base folder.</summary>
    /// <param name="applicationBaseDirectory">The build program's base folder; <c>null</c> means
    /// <see cref="AppContext.BaseDirectory"/>.</param>
    public static string GetArchivePath(string applicationBaseDirectory = null) => Path.Combine(GetDirectory(applicationBaseDirectory), ArchiveFileName);

    /// <summary>The notices file below an application's base folder.</summary>
    /// <param name="applicationBaseDirectory">The build program's base folder; <c>null</c> means
    /// <see cref="AppContext.BaseDirectory"/>.</param>
    public static string GetNoticesPath(string applicationBaseDirectory = null) => Path.Combine(GetDirectory(applicationBaseDirectory), NoticesFileName);

    /// <summary>
    /// The third-party notices for everything the platform puts into an application's
    /// WebAssembly file: the platform's crates (each with its licence and licence text) and the
    /// third-party code in the shared crate. An application adds its own notices (for crates it
    /// adds, its stylesheet tool and so on) and serves the whole with its site.
    /// </summary>
    /// <param name="applicationBaseDirectory">The build program's base folder; <c>null</c> means
    /// <see cref="AppContext.BaseDirectory"/>.</param>
    /// <returns>The text (UTF-8, line feeds).</returns>
    /// <exception cref="FrontendBuildException">The notices file is not beside the build program
    /// (the package was not restored, or the program was copied without its frontend-crates
    /// folder).</exception>
    public static string ReadNotices(string applicationBaseDirectory = null)
    {
        var path = GetNoticesPath(applicationBaseDirectory);
        if (!File.Exists(path))
        {
            throw new FrontendBuildException(MissingMessage(path));
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>
    /// The platform's <c>Cargo.lock</c>: the versions of the platform's crates.
    /// <see cref="PlatformFrontend.MaterializeAsync"/> writes it into a frontend that has no lock
    /// file yet, so that cargo starts from the platform's versions; from then on the lock file is
    /// the application's own.
    /// </summary>
    public static string LockFileText
    {
        get
        {
            using var stream = typeof(PlatformCrates).GetTypeInfo().Assembly.GetManifestResourceStream(LockResourceName)
                ?? throw new InvalidOperationException($"The resource '{LockResourceName}' is missing from the assembly.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Makes a folder hold exactly the crates of an archive. Nothing is done when the folder's
    /// stamp says it was written from the same archive; otherwise the archive is unpacked next
    /// to it and swapped in, and the stamp is written last.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="directory">The vendor folder; it belongs to this method.</param>
    /// <returns>True when the folder was (re)written.</returns>
    /// <exception cref="FrontendBuildException">The archive is missing or cannot be read, or the
    /// folder holds something this library did not write.</exception>
    internal static bool ExtractTo(string archive, string directory)
    {
        if (!File.Exists(archive))
        {
            throw new FrontendBuildException(MissingMessage(archive));
        }

        var root = Path.GetFullPath(directory);
        var stamp = Path.Combine(root, StampFileName);
        RequireOwned(root);
        string hash;
        using (var stream = File.OpenRead(archive))
        {
            hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        if (File.Exists(stamp) && string.Equals(File.ReadAllText(stamp).Trim(), hash, StringComparison.Ordinal))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(root);
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, "." + Path.GetFileName(root) + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(archive, staging);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(staging);
            throw new FrontendBuildException($"The platform's crates could not be unpacked from '{archive}': {exception.Message}", exception);
        }

        File.WriteAllText(Path.Combine(staging, StampFileName), hash + "\n");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.Move(staging, root);
        return true;
    }

    /// <summary>
    /// Removes a vendor folder this library wrote (it carries the stamp). Does nothing when the
    /// folder does not exist.
    /// </summary>
    /// <param name="directory">The vendor folder.</param>
    /// <returns>True when a folder was removed.</returns>
    /// <exception cref="FrontendBuildException">The folder holds something this library did not write.</exception>
    internal static bool Remove(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            return false;
        }

        RequireOwned(root);
        Directory.Delete(root, recursive: true);
        return true;
    }

    /// <summary>The crate folders (name-version) of a materialized vendor folder, sorted.</summary>
    /// <param name="directory">The vendor folder.</param>
    internal static IReadOnlyList<string> ListCrates(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).Select(Path.GetFileName).Where(n => !n.StartsWith('.')).OrderBy(n => n, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();

    private static void RequireOwned(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !File.Exists(Path.Combine(root, StampFileName)))
        {
            throw new FrontendBuildException(
                $"The vendor folder '{root}' holds files that CodeBrix.WebPlatform.FrontendBuilder did not write (it has no {StampFileName} file). Nothing was written. Give the platform's crates a folder of their own (PlatformFrontendOptions.VendorDirectory), or remove that folder.");
        }
    }

    private static string MissingMessage(string path)
        => $"'{path}' does not exist. The CodeBrix.WebPlatform.FrontendBuilder package copies its {FolderName} folder beside the build program when the program is built; restore and build the build program again.";

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort: a staging folder starts with a dot and is never read.
        }
    }
}
