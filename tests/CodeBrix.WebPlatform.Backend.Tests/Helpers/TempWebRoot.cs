using System;
using System.IO;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>
/// A content root under the system temp folder with a web root (wwwroot) inside it and, once a
/// built file is written, the built frontend (frontend-built), deleted when disposed. It writes
/// the small fake frontends the serving tests are about, and the developer's own files.
/// </summary>
public sealed class TempWebRoot : IDisposable
{
    public const string DefaultIndex = "<!doctype html><html><body>default application</body></html>";

    public const string OwnRootIndex = "<!doctype html><html><body>the developer's own root page</body></html>";

    public static readonly byte[] WasmBytes = { 0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00 };

    public TempWebRoot(bool createWebRoot = true)
    {
        ContentRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cbwp-backend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRoot);
        Path = System.IO.Path.Combine(ContentRoot, "wwwroot");
        BuiltPath = System.IO.Path.Combine(ContentRoot, BuiltFrontend.FolderName);
        if (createWebRoot)
        {
            Directory.CreateDirectory(Path);
        }
    }

    public string ContentRoot { get; }

    /// <summary>The web root (wwwroot): the developer's own files.</summary>
    public string Path { get; }

    /// <summary>The built frontend (frontend-built): what the frontend build writes.</summary>
    public string BuiltPath { get; }

    public static string NamedIndex(string name) => $"<!doctype html><html><body>{name} application</body></html>";

    /// <summary>Writes a file of the built frontend.</summary>
    public string WriteBuilt(string relativePath, string content)
    {
        var full = Full(BuiltPath, relativePath);
        File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Writes a file of the built frontend.</summary>
    public string WriteBuilt(string relativePath, byte[] content)
    {
        var full = Full(BuiltPath, relativePath);
        File.WriteAllBytes(full, content);
        return full;
    }

    /// <summary>Writes one of the developer's own files into the web root (creating it when it is missing).</summary>
    public string WriteOwn(string relativePath, string content)
    {
        var full = Full(Path, relativePath);
        File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Deletes one of the developer's own files from the web root.</summary>
    public void DeleteOwn(string relativePath) => File.Delete(Full(Path, relativePath));

    /// <summary>Deletes the web root folder with everything in it.</summary>
    public void DeleteWebRoot() => Directory.Delete(Path, recursive: true);

    /// <summary>
    /// Writes what a frontend build of these applications leaves in the built frontend: each
    /// application's index.html, a .wasm file and a stylesheet in its folder, and (unless told not
    /// to) the manifest, written by the frontend build's own writer.
    /// </summary>
    public void WriteBuiltFrontend(FrontendApplicationSet applications, bool writeManifest = true)
    {
        foreach (var application in applications)
        {
            var folder = application.Folder.Length == 0 ? string.Empty : application.Folder + "/";
            WriteBuilt(folder + "index.html", application.IsDefault ? DefaultIndex : NamedIndex(application.Name));
            WriteBuilt(folder + "app.wasm", WasmBytes);
            WriteBuilt(folder + "assets/styles.css", "body { margin: 0; }");
        }

        if (writeManifest)
        {
            FrontendManifestWriter.Write(BuiltPath, applications);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(ContentRoot))
        {
            Directory.Delete(ContentRoot, recursive: true);
        }
    }

    private static string Full(string folder, string relativePath)
    {
        var full = System.IO.Path.Combine(folder, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        return full;
    }
}
