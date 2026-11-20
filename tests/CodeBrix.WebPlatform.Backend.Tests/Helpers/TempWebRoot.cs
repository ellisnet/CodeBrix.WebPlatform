using System;
using System.IO;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>
/// A content root under the system temp folder with a web root (wwwroot) inside it, deleted when
/// disposed. It writes the small fake frontends the serving tests are about.
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
        if (createWebRoot)
        {
            Directory.CreateDirectory(Path);
        }
    }

    public string ContentRoot { get; }

    /// <summary>The web root.</summary>
    public string Path { get; }

    public static string NamedIndex(string name) => $"<!doctype html><html><body>{name} application</body></html>";

    public string Write(string relativePath, string content)
    {
        var full = Full(relativePath);
        File.WriteAllText(full, content);
        return full;
    }

    public string Write(string relativePath, byte[] content)
    {
        var full = Full(relativePath);
        File.WriteAllBytes(full, content);
        return full;
    }

    /// <summary>
    /// Writes what a frontend build of these applications leaves in the web root: each
    /// application's index.html, a .wasm file and a stylesheet in its folder, and (unless told not
    /// to) the manifest, written by the frontend build's own writer.
    /// </summary>
    public void WriteBuiltFrontend(FrontendApplicationSet applications, bool writeManifest = true)
    {
        foreach (var application in applications)
        {
            var folder = application.Folder.Length == 0 ? string.Empty : application.Folder + "/";
            Write(folder + "index.html", application.IsDefault ? DefaultIndex : NamedIndex(application.Name));
            Write(folder + "app.wasm", WasmBytes);
            Write(folder + "assets/styles.css", "body { margin: 0; }");
        }

        if (writeManifest)
        {
            FrontendManifestWriter.Write(Path, applications);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(ContentRoot))
        {
            Directory.Delete(ContentRoot, recursive: true);
        }
    }

    private string Full(string relativePath)
    {
        var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        return full;
    }
}
