using System;
using System.IO;

namespace WorkSite.BuildFrontend.Tests.Helpers;

/// <summary>
/// A throw-away folder under the system temporary folder, removed when disposed.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "worksite-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string relativePath, string contents)
    {
        var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        File.WriteAllText(full, contents);
        return full;
    }

    public string Read(string relativePath) => File.ReadAllText(System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    public bool Exists(string relativePath) => File.Exists(System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
