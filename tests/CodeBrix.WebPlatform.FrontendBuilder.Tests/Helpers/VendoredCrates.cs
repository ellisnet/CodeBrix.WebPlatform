using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;

/// <summary>
/// Tiny made-up crates in the form `cargo vendor` writes (a folder name-version with Cargo.toml,
/// src/lib.rs and a .cargo-checksum.json of real SHA-256 sums with "package": null), which the
/// installed cargo really builds from; and running cargo for the tests that need it.
/// </summary>
public static class VendoredCrates
{
    /// <summary>The bytes of an archive of tiny crates that cargo accepts.</summary>
    public static byte[] Archive(params Bundles.Crate[] crates)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var crate in crates)
            {
                var folder = crate.Name + "-" + crate.Version + "/";
                var manifest = $"[package]\nedition = \"2021\"\nname = \"{crate.Name}\"\nversion = \"{crate.Version}\"\n";
                var library = $"pub fn version() -> &'static str {{ \"{crate.Version}\" }}\n";
                Add(zip, folder + "Cargo.toml", manifest);
                Add(zip, folder + "src/lib.rs", library);
                Add(zip, folder + ".cargo-checksum.json", "{\"files\":{\"Cargo.toml\":\"" + Sha256(manifest) + "\",\"src/lib.rs\":\"" + Sha256(library) + "\"},\"package\":null}");
            }
        }

        return memory.ToArray();
    }

    /// <summary>A bundle of tiny crates that cargo accepts.</summary>
    public static CrateBundle Bundle(string name, string package, string version, params Bundles.Crate[] crates)
    {
        var bytes = Archive(crates);
        return new CrateBundle(name, package, version, () => new MemoryStream(bytes, writable: false));
    }

    /// <summary>Writes the platform's stand-in archive of tiny crates where the build program's is looked for.</summary>
    public static string WritePlatformArchive(TempFolder folder, params Bundles.Crate[] crates)
    {
        var path = CodeBrix.WebPlatform.FrontendBuilder.Crates.PlatformCrates.GetArchivePath(folder.Combine("build-program"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, Archive(crates));
        return path;
    }

    /// <summary>
    /// Writes a frontend crate that depends on the named crates (each "name = requirement") and
    /// uses each of them, and returns its folder.
    /// </summary>
    public static string WriteFrontend(TempFolder folder, string relativePath, params (string Name, string Requirement)[] dependencies)
    {
        var manifest = new StringBuilder("[package]\nname = \"lock-test-frontend\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[dependencies]\n");
        var library = new StringBuilder();
        foreach (var (name, requirement) in dependencies)
        {
            manifest.Append(name).Append(" = \"").Append(requirement).Append("\"\n");
            library.Append("pub fn ").Append(name.Replace('-', '_')).Append("_version() -> &'static str { ").Append(name.Replace('-', '_')).Append("::version() }\n");
        }

        folder.Write(relativePath + "/Cargo.toml", manifest.ToString());
        folder.Write(relativePath + "/src/lib.rs", library.ToString());
        return folder.Combine(relativePath);
    }

    /// <summary>Whether cargo can be started on this machine.</summary>
    public static bool CargoIsInstalled => CargoInstalled.Value;

    /// <summary>Runs cargo in a folder and returns the result.</summary>
    internal static Task<ProcessResult> CargoAsync(string directory, CancellationToken cancellationToken, params string[] arguments)
        => new ProcessRunner(null).RunAsync("cargo", arguments, directory, echo: false, cancellationToken: cancellationToken);

    private static readonly Lazy<bool> CargoInstalled = new Lazy<bool>(() =>
        new ProcessRunner(null).RunAsync("cargo", new[] { "--version" }, echo: false).GetAwaiter().GetResult().Succeeded);

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
