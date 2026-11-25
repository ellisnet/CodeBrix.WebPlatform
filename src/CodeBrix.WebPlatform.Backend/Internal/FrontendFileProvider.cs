using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// The files the backend serves: the built frontend's (<see cref="Built"/>: <c>frontend-built/</c>
/// without its manifest, its <c>.gitignore</c> and its compressed copies) and the developer's own
/// (<see cref="Own"/>: the web root, <c>wwwroot/</c>, without the offline file and without a
/// file of the manifest's name at its top). When an address names a file in both, the side
/// <see cref="FrontendFilePrecedence"/> says wins.
/// </summary>
internal sealed class FrontendFileProvider : IFileProvider
{
    private readonly bool _builtWins;
    private readonly string _builtPrefix;

    /// <summary>The files of both folders.</summary>
    /// <param name="built">The built frontend's files.</param>
    /// <param name="builtFrontendPath">The built frontend's full path.</param>
    /// <param name="webRoot">The web root's files (the host's web root provider).</param>
    /// <param name="precedence">Which side wins at an address both have.</param>
    public FrontendFileProvider(IFileProvider built, string builtFrontendPath, IFileProvider webRoot, FrontendFilePrecedence precedence)
    {
        ArgumentNullException.ThrowIfNull(built);
        ArgumentException.ThrowIfNullOrEmpty(builtFrontendPath);
        Built = new ManifestHidingFileProvider(built, builtFrontendPath);
        Own = new OwnFileProvider(webRoot ?? new NullFileProvider());
        _builtWins = precedence != FrontendFilePrecedence.WebRootWins;
        _builtPrefix = Path.GetFullPath(builtFrontendPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    /// <summary>The built frontend's files.</summary>
    public ManifestHidingFileProvider Built { get; }

    /// <summary>The developer's own files of the web root.</summary>
    public IFileProvider Own { get; }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        var first = _builtWins ? Built : Own;
        var second = _builtWins ? Own : Built;
        var info = first.GetFileInfo(subpath);
        return info.Exists ? info : second.GetFileInfo(subpath);
    }

    /// <summary>
    /// The built frontend's file at an address when it is the one served there; null when the
    /// built frontend has no file there or the web root's wins.
    /// </summary>
    public IFileInfo GetServedBuiltFile(string subpath)
    {
        var built = Built.GetFileInfo(subpath);
        if (!built.Exists || built.IsDirectory)
        {
            return null;
        }

        return _builtWins || !Own.GetFileInfo(subpath).Exists ? built : null;
    }

    /// <summary>True when a file this provider gave out is one of the built frontend's.</summary>
    public bool IsBuiltFile(IFileInfo file)
        => file != null && !string.IsNullOrEmpty(file.PhysicalPath)
           && file.PhysicalPath.StartsWith(_builtPrefix, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var first = (_builtWins ? Built : Own).GetDirectoryContents(subpath);
        var second = (_builtWins ? Own : Built).GetDirectoryContents(subpath);
        if (!first.Exists)
        {
            return second;
        }

        return second.Exists ? new MergedContents(first, second) : first;
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter)
        => new CompositeChangeToken(new[] { Built.Watch(filter), Own.Watch(filter) });

    /// <summary>
    /// The addresses (from the web root's top, with a leading slash, sorted) of the files the web
    /// root and the built frontend both have.
    /// </summary>
    public IReadOnlyList<string> FindClashes()
    {
        var clashes = new List<string>();
        Collect("", clashes);
        clashes.Sort(StringComparer.Ordinal);
        return clashes;
    }

    private void Collect(string folder, List<string> clashes)
    {
        var contents = Own.GetDirectoryContents(folder);
        if (!contents.Exists)
        {
            return;
        }

        foreach (var entry in contents)
        {
            var address = folder + "/" + entry.Name;
            if (entry.IsDirectory)
            {
                Collect(address, clashes);
            }
            else
            {
                var built = Built.GetFileInfo(address);
                if (built.Exists && !built.IsDirectory)
                {
                    clashes.Add(address);
                }
            }
        }
    }

    /// <summary>The web root's files without the offline file and the manifest's name at its top.</summary>
    private sealed class OwnFileProvider : IFileProvider
    {
        private readonly IFileProvider _inner;

        public OwnFileProvider(IFileProvider inner) => _inner = inner;

        public IFileInfo GetFileInfo(string subpath)
            => IsHidden(subpath) ? new NotFoundFileInfo(subpath ?? string.Empty) : _inner.GetFileInfo(subpath);

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var contents = _inner.GetDirectoryContents(subpath);
            if (!contents.Exists || !string.IsNullOrEmpty(subpath?.Replace('\\', '/').Trim('/')))
            {
                return contents;
            }

            return new ListedContents(contents.Where(f => f.IsDirectory || !IsHidden(f.Name)).ToList());
        }

        public IChangeToken Watch(string filter) => _inner.Watch(filter);

        private static bool IsHidden(string subpath)
            => ManifestHidingFileProvider.IsRootAddress(subpath, PlatformBackendPaths.OfflineFileName)
               || ManifestHidingFileProvider.IsRootAddress(subpath, FrontendManifest.FileName);
    }

    private sealed class MergedContents : IDirectoryContents
    {
        private readonly IDirectoryContents _first;
        private readonly IDirectoryContents _second;

        public MergedContents(IDirectoryContents first, IDirectoryContents second)
        {
            _first = first;
            _second = second;
        }

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            return _first.Concat(_second).Where(f => names.Add(f.Name)).ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ListedContents : IDirectoryContents
    {
        private readonly List<IFileInfo> _files;

        public ListedContents(List<IFileInfo> files) => _files = files;

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() => _files.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
