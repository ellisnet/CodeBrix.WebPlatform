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
/// The built frontend's files (<c>frontend-built/</c>) without the frontend manifest, without the
/// folder's own <c>.gitignore</c> and without the compressed copies the frontend build writes.
/// The manifest is recognised both by the address asked for (its name at the folder's top in any
/// casing, also with the trailing dots, spaces or stream suffix a Windows file system ignores)
/// and by the physical file it resolves to, so no spelling of the address reaches it; the
/// <c>.gitignore</c> by its address, in the same way. A copy is a file whose name ends in <c>.br</c> or <c>.gz</c> (any casing, with
/// the same Windows spellings) beside a file of the name without that suffix: it is an alternate
/// of that file, served only in its place (<see cref="GetCopy"/>), never under its own name. A
/// <c>.br</c> / <c>.gz</c> file with no such file beside it is an ordinary file.
/// </summary>
internal sealed class ManifestHidingFileProvider : IFileProvider
{
    private readonly IFileProvider _inner;
    private readonly string _manifestPath;

    /// <summary>The name of the file the frontend build writes to keep the folder out of git.</summary>
    public const string GitIgnoreFileName = ".gitignore";

    /// <summary>The built frontend's files, read through <paramref name="inner"/>.</summary>
    /// <param name="inner">The folder's files.</param>
    /// <param name="builtFrontendPath">The folder's full path, to know the manifest's physical file; null when there is no folder.</param>
    public ManifestHidingFileProvider(IFileProvider inner, string builtFrontendPath)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _manifestPath = string.IsNullOrEmpty(builtFrontendPath)
            ? null
            : Path.GetFullPath(Path.Combine(builtFrontendPath, FrontendManifest.FileName));
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        var info = _inner.GetFileInfo(subpath);
        return IsManifestAddress(subpath) || IsRootAddress(subpath, GitIgnoreFileName) || IsManifestFile(info) || IsCopyAddress(subpath)
            ? new NotFoundFileInfo(subpath ?? string.Empty)
            : info;
    }

    /// <summary>
    /// The compressed copy of a file, by the file's address and the copy's suffix
    /// (<c>.br</c> or <c>.gz</c>); a file that does not exist when there is none. The address
    /// must be one that <see cref="GetFileInfo"/> answers with an existing file.
    /// </summary>
    public IFileInfo GetCopy(string subpath, string suffix)
    {
        var info = _inner.GetFileInfo(subpath + suffix);
        return info.Exists && !info.IsDirectory ? info : new NotFoundFileInfo(subpath + suffix);
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = _inner.GetDirectoryContents(subpath);
        return contents.Exists ? new FilteredContents(contents, this, string.IsNullOrEmpty(subpath?.Replace('\\', '/').Trim('/'))) : contents;
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) => _inner.Watch(filter);

    /// <summary>True when an address names the manifest at the folder's top.</summary>
    internal static bool IsManifestAddress(string subpath) => IsRootAddress(subpath, FrontendManifest.FileName);

    /// <summary>
    /// True when an address names a file of a name at the top of its folder, in any casing, also
    /// with the trailing dots, spaces or stream suffix a Windows file system ignores.
    /// </summary>
    internal static bool IsRootAddress(string subpath, string fileName)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return false;
        }

        var trimmed = subpath.Replace('\\', '/').Trim('/');
        if (trimmed.Contains('/'))
        {
            return false;
        }

        var colon = trimmed.IndexOf(':');
        if (colon >= 0)
        {
            trimmed = trimmed.Substring(0, colon);
        }

        trimmed = trimmed.TrimEnd('.', ' ');
        return string.Equals(trimmed, fileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when an address names a compressed copy: its last segment ends in <c>.br</c> or
    /// <c>.gz</c> and the address without that suffix is a file.
    /// </summary>
    internal bool IsCopyAddress(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return false;
        }

        var normalized = subpath.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        var folder = normalized.Substring(0, slash + 1);
        var name = normalized.Substring(slash + 1);
        var colon = name.IndexOf(':');
        if (colon >= 0)
        {
            name = name.Substring(0, colon);
        }

        name = name.TrimEnd('.', ' ');
        if (name.Length <= 3 || !IsCopyName(name))
        {
            return false;
        }

        var original = _inner.GetFileInfo(folder + name.Substring(0, name.Length - 3));
        return original.Exists && !original.IsDirectory;
    }

    private static bool IsCopyName(string name)
        => name.EndsWith(".br", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

    private bool IsManifestFile(IFileInfo info)
    {
        if (_manifestPath == null || !info.Exists || string.IsNullOrEmpty(info.PhysicalPath))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(info.PhysicalPath), _manifestPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
        {
            return false;
        }
    }

    private sealed class FilteredContents : IDirectoryContents
    {
        private readonly IDirectoryContents _inner;
        private readonly ManifestHidingFileProvider _owner;
        private readonly bool _top;

        public FilteredContents(IDirectoryContents inner, ManifestHidingFileProvider owner, bool top)
        {
            _inner = inner;
            _owner = owner;
            _top = top;
        }

        public bool Exists => _inner.Exists;

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            var files = _inner.ToList();
            var names = new HashSet<string>(files.Where(f => !f.IsDirectory).Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            return files.Where(f => !_owner.IsManifestFile(f) && !IsCopyOf(f, names)
                    && !(_top && string.Equals(f.Name, GitIgnoreFileName, StringComparison.OrdinalIgnoreCase)))
                .GetEnumerator();
        }

        private static bool IsCopyOf(IFileInfo file, HashSet<string> names)
            => !file.IsDirectory && file.Name.Length > 3 && IsCopyName(file.Name) && names.Contains(file.Name.Substring(0, file.Name.Length - 3));

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
