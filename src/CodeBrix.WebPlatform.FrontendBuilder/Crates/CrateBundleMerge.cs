using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBrix.WebPlatform.Core.Crates;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// Puts the crates of the registered bundles together with the platform's crates, under the
/// strict rule: a crate is identified by name and version; the same name and version must have
/// the same content wherever it comes from (one copy is kept); and a bundle must not bring a
/// version of a crate that the platform or another bundle carries in a different version.
/// </summary>
internal sealed class CrateBundleMerge
{
    /// <summary>What the platform's own crates are called in messages.</summary>
    internal const string PlatformOrigin = "the platform's crates (CodeBrix.WebPlatform.FrontendBuilder)";

    private const string ChecksumFileName = ".cargo-checksum.json";
    private const string ManifestFileName = "Cargo.toml";

    private readonly List<LoadedBundle> _bundles = new List<LoadedBundle>();

    private CrateBundleMerge(string platformArchive, string platformHash)
    {
        PlatformArchive = platformArchive;
        PlatformHash = platformHash;
    }

    /// <summary>The platform's archive.</summary>
    internal string PlatformArchive { get; }

    /// <summary>The SHA-256 of the platform's archive, lowercase hexadecimal.</summary>
    internal string PlatformHash { get; }

    /// <summary>What each bundle adds and shares, in registration order.</summary>
    internal IReadOnlyList<CrateBundleOutcome> Outcomes { get; private set; } = Array.Empty<CrateBundleOutcome>();

    /// <summary>
    /// What identifies the content of a vendor folder written from the platform's archive and
    /// these bundles: the platform archive's hash alone when there is no bundle, otherwise a
    /// hash over it and every bundle's name, origin and archive hash, in registration order.
    /// </summary>
    internal string Stamp
    {
        get
        {
            if (_bundles.Count == 0)
            {
                return PlatformHash;
            }

            var text = new StringBuilder("platform ").Append(PlatformHash).Append('\n');
            foreach (var bundle in _bundles)
            {
                text.Append("bundle ").Append(bundle.Bundle.Name).Append('\t').Append(bundle.Bundle.Origin).Append('\t').Append(bundle.Hash).Append('\n');
            }

            return "bundles-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Reads the platform's archive and every bundle, and applies the strict rule.
    /// </summary>
    /// <param name="platformArchive">The platform's archive of crates.</param>
    /// <param name="bundles">The registered bundles, in registration order.</param>
    /// <returns>The checked merge.</returns>
    /// <exception cref="FrontendBuildException">The platform's archive is missing, a bundle
    /// cannot be read or is not an archive of crates, or the bundles break the strict rule (one
    /// message lists every crate that does).</exception>
    internal static CrateBundleMerge Check(string platformArchive, IReadOnlyList<CrateBundle> bundles)
    {
        if (!File.Exists(platformArchive))
        {
            throw new FrontendBuildException(PlatformCrates.MissingMessage(platformArchive));
        }

        string platformHash;
        using (var stream = File.OpenRead(platformArchive))
        {
            platformHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        var merge = new CrateBundleMerge(platformArchive, platformHash);
        if (bundles == null || bundles.Count == 0)
        {
            return merge;
        }

        // name -> version -> (content, origin) of every crate seen so far, the platform's first.
        var seen = new Dictionary<string, Dictionary<string, SeenCrate>>(StringComparer.Ordinal);
        try
        {
            using var platform = ZipFile.OpenRead(platformArchive);
            foreach (var crate in ReadCrates(platform, PlatformOrigin))
            {
                Versions(seen, crate.Name)[crate.Version] = new SeenCrate(crate.Checksum, PlatformOrigin);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new FrontendBuildException($"The platform's crates could not be read from '{platformArchive}': {exception.Message}", exception);
        }

        var problems = new List<string>();
        var outcomes = new List<CrateBundleOutcome>();
        foreach (var bundle in bundles)
        {
            var loaded = Load(bundle);
            var where = $"the crate bundle '{bundle.Name}' of {bundle.Origin}";
            var added = new List<string>();
            var shared = new List<string>();
            foreach (var crate in loaded.Crates)
            {
                var versions = Versions(seen, crate.Name);
                if (versions.TryGetValue(crate.Version, out var same))
                {
                    if (!same.Checksum.AsSpan().SequenceEqual(crate.Checksum))
                    {
                        problems.Add($"{crate.Name} {crate.Version}: {where} and {same.Origin} both carry it, with different content.");
                    }
                    else
                    {
                        shared.Add(crate.Name + " " + crate.Version);
                    }

                    continue;
                }

                var other = versions.FirstOrDefault(v => !string.Equals(v.Value.Origin, where, StringComparison.Ordinal));
                if (other.Key != null)
                {
                    problems.Add($"{crate.Name}: {where} carries version {crate.Version}, but {other.Value.Origin} carries version {other.Key}.");
                    continue;
                }

                versions[crate.Version] = new SeenCrate(crate.Checksum, where);
                loaded.Add.Add(crate.Folder);
                added.Add(crate.Name + " " + crate.Version);
            }

            added.Sort(StringComparer.Ordinal);
            shared.Sort(StringComparer.Ordinal);
            outcomes.Add(new CrateBundleOutcome(bundle.Name, bundle.Origin, added, shared));
            merge._bundles.Add(loaded);
        }

        if (problems.Count > 0)
        {
            throw new FrontendBuildException(
                "The registered crate bundles cannot be combined with the platform's crates. Every package that brings a crate must bring the same version of it, with the same content. Nothing was written."
                + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  - " + p))
                + Environment.NewLine + "Use versions of those packages that were built against the same platform version, or leave one of them out.");
        }

        merge.Outcomes = outcomes;
        return merge;
    }

    /// <summary>
    /// Writes into a folder (which must not exist yet) the platform's crates and, of every
    /// bundle, the crates only it brings.
    /// </summary>
    /// <param name="directory">The folder to create.</param>
    internal void ExtractInto(string directory)
    {
        ZipFile.ExtractToDirectory(PlatformArchive, directory);
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        foreach (var bundle in _bundles)
        {
            if (bundle.Add.Count == 0)
            {
                continue;
            }

            using var archive = new ZipArchive(new MemoryStream(bundle.Bytes, writable: false), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                var slash = path.IndexOf('/');
                if (slash <= 0 || !bundle.Add.Contains(path.Substring(0, slash)) || path.EndsWith('/'))
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine(directory, path));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"The entry '{entry.FullName}' of the crate bundle '{bundle.Bundle.Name}' of {bundle.Bundle.Origin} leaves the archive's folder.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, overwrite: false);
            }
        }
    }

    private static Dictionary<string, SeenCrate> Versions(Dictionary<string, Dictionary<string, SeenCrate>> seen, string name)
    {
        if (!seen.TryGetValue(name, out var versions))
        {
            versions = new Dictionary<string, SeenCrate>(StringComparer.Ordinal);
            seen[name] = versions;
        }

        return versions;
    }

    private static LoadedBundle Load(CrateBundle bundle)
    {
        var where = $"the crate bundle '{bundle.Name}' of {bundle.Origin}";
        byte[] bytes;
        try
        {
            using var stream = bundle.OpenArchive();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            throw new FrontendBuildException($"The archive of {where} could not be opened: {exception.Message} Restore and build the build program again.", exception);
        }

        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            var crates = ReadCrates(archive, where);
            if (crates.Count == 0)
            {
                throw new FrontendBuildException($"The archive of {where} holds no crates. An archive of crates has one folder per crate, each with its {ManifestFileName} and {ChecksumFileName}.");
            }

            return new LoadedBundle(bundle, bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), crates);
        }
        catch (InvalidDataException exception)
        {
            throw new FrontendBuildException($"The archive of {where} is not a readable zip file: {exception.Message}", exception);
        }
    }

    /// <summary>The crates of an archive: one per top-level folder.</summary>
    private static List<ArchivedCrate> ReadCrates(ZipArchive archive, string where)
    {
        var manifests = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var checksums = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var folders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            var slash = path.IndexOf('/');
            if (slash <= 0)
            {
                if (path.Length > 0 && !path.StartsWith('.'))
                {
                    throw new FrontendBuildException($"The archive of {where} has the file '{path}' outside any crate's folder. An archive of crates holds only one folder per crate.");
                }

                continue;
            }

            var folder = path.Substring(0, slash);
            if (folder.StartsWith('.'))
            {
                continue;
            }

            folders.Add(folder);
            var rest = path.Substring(slash + 1);
            if (rest == ManifestFileName)
            {
                manifests[folder] = entry;
            }
            else if (rest == ChecksumFileName)
            {
                checksums[folder] = entry;
            }
        }

        var crates = new List<ArchivedCrate>();
        foreach (var folder in folders)
        {
            if (!manifests.TryGetValue(folder, out var manifest) || !checksums.TryGetValue(folder, out var checksum))
            {
                throw new FrontendBuildException($"The folder '{folder}' in the archive of {where} is not a vendored crate: it needs a {ManifestFileName} and a {ChecksumFileName} (as `cargo vendor` writes them).");
            }

            var (name, version) = ReadPackage(ReadText(manifest));
            if (name == null || version == null)
            {
                throw new FrontendBuildException($"The {ManifestFileName} of '{folder}' in the archive of {where} names no [package] name and version.");
            }

            crates.Add(new ArchivedCrate(folder, name, version, ReadBytes(checksum)));
        }

        return crates;
    }

    /// <summary>The name and version in the [package] table of a Cargo.toml as crates.io publishes it.</summary>
    internal static (string Name, string Version) ReadPackage(string manifest)
    {
        string name = null;
        string version = null;
        var inPackage = false;
        foreach (var raw in manifest.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                if (inPackage)
                {
                    break;
                }

                inPackage = line == "[package]";
                continue;
            }

            if (!inPackage)
            {
                continue;
            }

            name ??= ReadString(line, "name");
            version ??= ReadString(line, "version");
        }

        return (name, version);
    }

    private static string ReadString(string line, string key)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = line.Substring(key.Length).TrimStart();
        if (!rest.StartsWith('='))
        {
            return null;
        }

        rest = rest.Substring(1).Trim();
        if (rest.Length < 2 || rest[0] != '"')
        {
            return null;
        }

        var end = rest.IndexOf('"', 1);
        return end < 0 ? null : rest.Substring(1, end - 1);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class SeenCrate
    {
        public SeenCrate(byte[] checksum, string origin)
        {
            Checksum = checksum;
            Origin = origin;
        }

        public byte[] Checksum { get; }

        public string Origin { get; }
    }

    private sealed class ArchivedCrate
    {
        public ArchivedCrate(string folder, string name, string version, byte[] checksum)
        {
            Folder = folder;
            Name = name;
            Version = version;
            Checksum = checksum;
        }

        public string Folder { get; }

        public string Name { get; }

        public string Version { get; }

        public byte[] Checksum { get; }
    }

    private sealed class LoadedBundle
    {
        public LoadedBundle(CrateBundle bundle, byte[] bytes, string hash, List<ArchivedCrate> crates)
        {
            Bundle = bundle;
            Bytes = bytes;
            Hash = hash;
            Crates = crates;
        }

        public CrateBundle Bundle { get; }

        public byte[] Bytes { get; }

        public string Hash { get; }

        public List<ArchivedCrate> Crates { get; }

        /// <summary>The folders of the crates only this bundle brings.</summary>
        public HashSet<string> Add { get; } = new HashSet<string>(StringComparer.Ordinal);
    }
}
