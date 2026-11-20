using System.IO;
using System.IO.Compression;
using System.Text;
using CodeBrix.WebPlatform.Core.Crates;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;

/// <summary>Small made-up crate bundles and crate archives.</summary>
public static class Bundles
{
    /// <summary>A made-up crate: its name, version and something that stands for its content.</summary>
    public sealed record Crate(string Name, string Version, string Content = "same");

    /// <summary>The bytes of an archive of made-up crates, one <c>name-version</c> folder each.</summary>
    public static byte[] Archive(params Crate[] crates)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var crate in crates)
            {
                var folder = crate.Name + "-" + crate.Version + "/";
                Add(zip, folder + "Cargo.toml", $"[package]\nedition = \"2021\"\nname = \"{crate.Name}\"\nversion = \"{crate.Version}\"\n\n[dependencies.other]\nversion = \"9\"\n");
                Add(zip, folder + ".cargo-checksum.json", "{\"files\":{\"src/lib.rs\":\"" + crate.Content + "\"},\"package\":\"" + crate.Content + "\"}");
                Add(zip, folder + "src/lib.rs", "// " + crate.Content + "\n");
            }
        }

        return memory.ToArray();
    }

    /// <summary>A bundle of made-up crates.</summary>
    public static CrateBundle Create(string name, string package, string version, params Crate[] crates)
    {
        var bytes = Archive(crates);
        return new CrateBundle(name, package, version, () => new MemoryStream(bytes, writable: false));
    }

    /// <summary>Writes an archive of made-up crates where the platform's archive is looked for and returns its path.</summary>
    public static string WritePlatformArchive(TempFolder folder, params Crate[] crates)
    {
        var path = CodeBrix.WebPlatform.FrontendBuilder.Crates.PlatformCrates.GetArchivePath(folder.Combine("build-program"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, Archive(crates));
        return path;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
