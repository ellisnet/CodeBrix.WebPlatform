using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CodeBrix.RustTools.Projects;
using CodeBrix.RustTools.Vendoring;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// Confirms that the two vendor folders are exact, untouched copies of what the lock files name,
/// that the shared crate's own lock file needs nothing the platform's crates lack, and that git
/// neither hides nor rewrites any file inside the vendored trees.
/// </summary>
public static class VendorCheck
{
    /// <summary>
    /// Verifies the platform's vendor folder (the crates the FrontendBuilder package ships)
    /// against the lock file of the platform's crate set.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static CheckResult Platform(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var report = VendorVerifier.Verify(layout.PlatformVendor, layout.PlatformCrateSetLock);
        return new CheckResult(
            "Vendored crates: the platform's crates are untouched and match the crate set's Cargo.lock",
            report.Issues.Select(i => i.Message),
            new[] { $"{report.CratesChecked} crates, {report.FilesChecked} files hashed" });
    }

    /// <summary>
    /// Confirms that every crate the shared crate's own Cargo.lock names (it serves the crate's
    /// <c>cargo test</c> in this repository) is one of the platform's crates, at the same
    /// version, so the shared crate never depends on anything an application would not get.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static CheckResult SharedCrate(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var platform = CargoLockFile.Load(layout.PlatformCrateSetLock).Packages
            .Where(p => !string.IsNullOrEmpty(p.Source))
            .Select(p => p.Name + " " + p.Version)
            .ToHashSet(StringComparer.Ordinal);
        var shared = CargoLockFile.Load(layout.SharedCrateLock).Packages.Where(p => !string.IsNullOrEmpty(p.Source)).ToList();
        var problems = shared
            .Where(p => !platform.Contains(p.Name + " " + p.Version))
            .Select(p => $"{p.Name} {p.Version} is in the shared crate's Cargo.lock but not among the platform's crates; add it to the crate set (Rust/platform-crates) and re-vendor, or align the version")
            .ToList();
        return new CheckResult(
            "Vendored crates: the shared crate's Cargo.lock names only the platform's crates",
            problems,
            new[] { $"{shared.Count} crates in the shared crate's Cargo.lock" });
    }

    /// <summary>
    /// Verifies the build tools' vendor folder, which is shared by the Dioxus CLI and the
    /// wasm-bindgen CLI: every crate either lock file names must be present and untouched, and
    /// every crate in the folder must be named by at least one of them.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static CheckResult Tooling(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var vendor = VendorFolder.Load(layout.ToolingVendor);
        var locks = new[]
        {
            CargoLockFile.Load(Path.Combine(layout.DioxusCli, CargoLockFile.FileName)),
            CargoLockFile.Load(Path.Combine(layout.WasmBindgenCli, CargoLockFile.FileName)),
        };
        var merged = new CargoLockFile { Packages = locks.SelectMany(l => l.Packages).ToList() };
        var report = VendorVerifier.Verify(vendor, merged);
        return new CheckResult(
            "Vendored crates: build-tool snapshot is untouched and matches both Cargo.lock files",
            report.Issues.Select(i => i.Message).Distinct(StringComparer.Ordinal),
            new[] { $"{report.CratesChecked} crates, {report.FilesChecked} files hashed" });
    }

    /// <summary>
    /// Asks git whether any file inside the vendored trees - the platform's crates, and the whole
    /// tool source folder (the two CLIs, their crates, the cargo configuration) - or any of
    /// their lock files is ignored. An ignored file would be missing from a fresh clone, and
    /// cargo refuses to build from a vendored crate with a missing file. Passes with a note when
    /// git is not installed or this is not a git checkout.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static CheckResult GitVisibility(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        const string name = "Vendored crates: git ignores no vendored or tool-source file";
        string output;
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = layout.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var paths = new[] { layout.PlatformVendor, layout.ToolSources, layout.PlatformCrateSetLock, layout.SharedCrateLock }.Select(layout.Relative);
            foreach (var argument in new[] { "ls-files", "--others", "--ignored", "--exclude-standard", "--" }.Concat(paths))
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return new CheckResult(name, Array.Empty<string>(), new[] { "not checked: this folder is not a git checkout" });
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new CheckResult(name, Array.Empty<string>(), new[] { "not checked: git is not installed" });
        }

        var ignored = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new CheckResult(name, ignored.Select(f => $"{f} is ignored by git and would be missing from a clone"));
    }
}
