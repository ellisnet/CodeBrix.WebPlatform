using System;
using System.IO;
using System.IO.Compression;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;

/// <summary>The fixture backend, and small frontend folders to build against.</summary>
public static class Frontends
{
    /// <summary>The sample backend the BackendMapper tests also read; it is built beside this assembly.</summary>
    public static string SampleBackendAssembly => Path.Combine(AppContext.BaseDirectory, "CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend.dll");

    /// <summary>The sample backend's project file, found by walking up to the repository root.</summary>
    public static string SampleBackendProject
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "tests", "CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend", "CodeBrix.WebPlatform.BackendMapper.Tests.SampleBackend.csproj");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("The sample backend's project was not found above " + AppContext.BaseDirectory);
        }
    }

    /// <summary>Writes a minimal frontend crate (Cargo.toml only) into a folder and returns the folder.</summary>
    public static string CreateFrontend(TempFolder folder, string packageName = "sample-frontend")
    {
        folder.Write("frontend/Cargo.toml", $"[package]\nname = \"{packageName}\"\nversion = \"0.1.0\"\nedition = \"2021\"\n");
        return folder.Combine("frontend");
    }

    /// <summary>Options for a frontend in a temporary folder that reads the sample backend.</summary>
    public static PlatformFrontendOptions CreateOptions(TempFolder folder)
        => new PlatformFrontendOptions { FrontendDirectory = CreateFrontend(folder), BackendAssemblyPath = SampleBackendAssembly };

    /// <summary>
    /// Writes a small stand-in for the package's crates archive (one fake crate) beside a
    /// stand-in build program, <c>build-program/frontend-crates/platform-crates.zip</c>, and
    /// returns its path. An existing one is kept unless <paramref name="crateName"/> differs.
    /// </summary>
    public static string WriteCratesArchive(TempFolder folder, string crateName = "fake-crate-1.0.0")
    {
        var archive = PlatformCrates.GetArchivePath(folder.Combine("build-program"));
        var source = folder.Combine("archive-source/" + crateName);
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Cargo.toml"), "[package]\nname = \"fake-crate\"\nversion = \"1.0.0\"\n");
        File.WriteAllText(Path.Combine(source, ".cargo-checksum.json"), "{\"files\":{},\"package\":\"0\"}");
        Directory.CreateDirectory(Path.GetDirectoryName(archive));
        if (File.Exists(archive))
        {
            File.Delete(archive);
        }

        ZipFile.CreateFromDirectory(Path.GetDirectoryName(source), archive);
        Directory.Delete(folder.Combine("archive-source"), recursive: true);
        return archive;
    }
}
