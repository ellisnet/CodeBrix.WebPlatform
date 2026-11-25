using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// The web root's files when the web root (<c>wwwroot/</c>) did not exist at start-up, so that
/// the host's own web-root provider finds nothing for the life of the process. Until the folder
/// exists every lookup answers "not found" (one existence check of the folder per lookup); the
/// first lookup that finds the folder makes a <see cref="PhysicalFileProvider"/> over it, which
/// is kept and reused from then on - also when the folder is deleted (its files are then not
/// found, without an exception) and created again. Disposed with the application.
/// </summary>
internal sealed class LateWebRootFileProvider : IFileProvider, IDisposable
{
    private readonly string _root;
    private PhysicalFileProvider _provider;
    private bool _disposed;

    /// <summary>The web root's files, read from <paramref name="webRootPath"/> once it exists.</summary>
    /// <param name="webRootPath">The web root's full path.</param>
    public LateWebRootFileProvider(string webRootPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(webRootPath);
        _root = Path.GetFullPath(webRootPath);
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
        => Current()?.GetFileInfo(subpath) ?? new NotFoundFileInfo(subpath ?? string.Empty);

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
        => Current()?.GetDirectoryContents(subpath) ?? NotFoundDirectoryContents.Singleton;

    /// <inheritdoc/>
    public IChangeToken Watch(string filter)
    {
        var provider = Current();
        if (provider == null)
        {
            return NullChangeToken.Singleton;
        }

        try
        {
            return provider.Watch(filter);
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException || e is PlatformNotSupportedException)
        {
            //The folder went away again: there is nothing to watch.
            return NullChangeToken.Singleton;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_root)
        {
            _disposed = true;
            _provider?.Dispose();
            _provider = null;
        }
    }

    private PhysicalFileProvider Current()
    {
        var provider = Volatile.Read(ref _provider);
        if (provider != null || !Directory.Exists(_root))
        {
            return provider;
        }

        lock (_root)
        {
            if (_provider == null && !_disposed)
            {
                try
                {
                    _provider = new PhysicalFileProvider(_root);
                }
                catch (DirectoryNotFoundException)
                {
                    //Deleted again between the check and here.
                }
            }

            return _provider;
        }
    }
}
