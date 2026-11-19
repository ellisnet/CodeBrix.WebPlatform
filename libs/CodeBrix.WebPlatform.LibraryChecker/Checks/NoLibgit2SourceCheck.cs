using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// Guards the rule that libgit2's source never enters the repository, in any form. The Dioxus
/// CLI reaches libgit2 through the <c>git2</c> crate, whose crates.io dependency
/// <c>libgit2-sys</c> carries libgit2's C source; the repository substitutes its own
/// declarations-only stand-in (<c>tools/dx-native-tools/sources/codebrix-libgit2-sys</c>), which
/// links the prebuilt libgit2 of the CodeBrix.LibGit2 package. The check confirms that the
/// stand-in holds no C or C++ source, header or assembly file and no sub-folder; that no vendor
/// folder holds a <c>libgit2-sys</c> crate; and that nowhere in the repository (outside git's own
/// folder and build output) is there a file named <c>git2.h</c> or a folder <c>include/git2</c>,
/// the marks of libgit2's source tree.
/// </summary>
public static class NoLibgit2SourceCheck
{
    /// <summary>The crates.io crate that carries libgit2's source.</summary>
    public const string UpstreamCrate = "libgit2-sys";

    /// <summary>The name of libgit2's public header, which only libgit2's own source tree holds.</summary>
    public const string LibgitHeader = "git2.h";

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c", ".h", ".cc", ".cpp", ".cxx", ".hh", ".hpp", ".hxx", ".inc", ".s", ".asm", ".m", ".mm",
    };

    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "target", "TestResults", ".vs", ".idea" };

    /// <summary>
    /// Runs the check on the repository.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    public static CheckResult Run(RepositoryLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Scan(layout.Root, layout.Libgit2StandIn, new[] { layout.ToolingVendor, layout.PlatformVendor });
    }

    /// <summary>
    /// Runs the check on a folder tree.
    /// </summary>
    /// <param name="root">The folder to scan for libgit2's source (the repository root).</param>
    /// <param name="standIn">The stand-in crate's folder.</param>
    /// <param name="vendorFolders">The vendor folders that must hold no <c>libgit2-sys</c> crate.</param>
    public static CheckResult Scan(string root, string standIn, IEnumerable<string> vendorFolders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(standIn);
        root = Path.GetFullPath(root);
        var problems = new List<string>();
        var shownStandIn = Path.GetRelativePath(root, Path.GetFullPath(standIn)).Replace('\\', '/');

        var standInFiles = 0;
        if (!File.Exists(Path.Combine(standIn, "Cargo.toml")))
        {
            problems.Add($"{shownStandIn}/Cargo.toml does not exist: the libgit2-sys stand-in is missing");
        }
        else
        {
            foreach (var directory in Directory.EnumerateDirectories(standIn).Where(d => !string.Equals(Path.GetFileName(d), "target", StringComparison.Ordinal)))
            {
                problems.Add($"{shownStandIn}/{Path.GetFileName(directory)}/ is a folder; the stand-in holds its few files only");
            }

            foreach (var file in Directory.EnumerateFiles(standIn))
            {
                standInFiles++;
                if (CodeExtensions.Contains(Path.GetExtension(file)))
                {
                    problems.Add($"{shownStandIn}/{Path.GetFileName(file)} is C, C++ or assembly code; the stand-in declares libgit2's functions in Rust and compiles no other code");
                }
            }
        }

        var vendored = 0;
        foreach (var vendor in (vendorFolders ?? Enumerable.Empty<string>()).Where(Directory.Exists))
        {
            foreach (var crate in Directory.EnumerateDirectories(vendor))
            {
                vendored++;
                var name = Path.GetFileName(crate);
                if (string.Equals(name, UpstreamCrate, StringComparison.OrdinalIgnoreCase) || name.StartsWith(UpstreamCrate + "-", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{Path.GetRelativePath(root, crate).Replace('\\', '/')} is the crates.io crate {UpstreamCrate}, which carries libgit2's C source; it must never be vendored");
                }
            }
        }

        var scanned = 0;
        Walk(root, root, problems, ref scanned);
        problems.Sort(StringComparer.Ordinal);
        return new CheckResult(
            "No libgit2 source: the libgit2-sys stand-in compiles no C code, and no crate or file of libgit2 is in the repository",
            problems,
            new[] { $"stand-in: {standInFiles} files; {vendored} vendored crate folders checked; {scanned} files scanned for libgit2's source" });
    }

    private static void Walk(string root, string directory, List<string> problems, ref int scanned)
    {
        foreach (var subdirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subdirectory);
            if (SkippedDirectoryNames.Contains(name))
            {
                continue;
            }

            if (string.Equals(name, "git2", StringComparison.Ordinal) && string.Equals(Path.GetFileName(directory), "include", StringComparison.Ordinal))
            {
                problems.Add($"{Path.GetRelativePath(root, subdirectory).Replace('\\', '/')}/ is libgit2's header folder");
            }

            Walk(root, subdirectory, problems, ref scanned);
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            scanned++;
            if (string.Equals(Path.GetFileName(file), LibgitHeader, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')} is libgit2's public header");
            }
        }
    }
}
