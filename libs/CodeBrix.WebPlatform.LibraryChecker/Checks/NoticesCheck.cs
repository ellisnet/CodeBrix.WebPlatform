using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.RustTools.Licensing;
using CodeBrix.RustTools.Notices;
using CodeBrix.RustTools.Projects;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// Writes, or checks, the repository's generated notices, each a hand-written opening followed by
/// a generated record of crates with their license texts:
/// <list type="bullet">
/// <item>the platform's crates (<c>THIRD-PARTY-NOTICES-PlatformCrates.txt</c>, packed into the
/// FrontendBuilder package): those compiled into a platform application, then the rest;</item>
/// <item>the two distributed build tools (<see cref="HostTools"/>): every crate compiled into the
/// program on at least one of the five platforms;</item>
/// <item>the tool sources (<c>THIRD-PARTY-NOTICES-ToolSources.txt</c>, repository-only): every
/// crate vendored to build the tools.</item>
/// </list>
/// </summary>
public static class NoticesCheck
{
    /// <summary>
    /// The distributed build tools whose notices are generated: the tool's package name, the
    /// program's name, and the name the files carry (<c>THIRD-PARTY-NOTICES-&lt;name&gt;.txt</c>,
    /// opened by <c>notices-header-&lt;name&gt;.txt</c>).
    /// </summary>
    public static readonly IReadOnlyList<(string Package, string Program, string FileName)> HostTools = new[]
    {
        (LicenseCheck.DioxusCliPackage, "dx", "Dioxus"),
        (LicenseCheck.WasmBindgenCliPackage, "wasm-bindgen", "WasmBindgen"),
    };

    /// <summary>
    /// The path of a distributed tool's generated notices file.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="fileName">The tool's file name part (see <see cref="HostTools"/>).</param>
    public static string HostToolNoticesFile(RepositoryLayout layout, string fileName)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Path.Combine(layout.HostToolsNoticesFolder, $"THIRD-PARTY-NOTICES-{fileName}.txt");
    }

    /// <summary>
    /// Builds a distributed tool's notices text: its hand-written opening, then every crate
    /// compiled into the program on at least one of the five platforms (from the per-platform
    /// audits), with each distinct license text once.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="audits">The license audits.</param>
    /// <param name="package">The tool's package name (<c>dioxus-cli</c> or <c>wasm-bindgen-cli</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="package"/> is not one of <see cref="HostTools"/>.</exception>
    /// <exception cref="FileNotFoundException">The tool's notices header file is missing.</exception>
    public static string GenerateHostToolNotices(RepositoryLayout layout, LicenseAudits audits, string package)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(audits);
        var tool = HostTools.FirstOrDefault(t => string.Equals(t.Package, package, StringComparison.Ordinal));
        if (tool.Package == null)
        {
            throw new ArgumentException($"'{package}' is not a distributed build tool.", nameof(package));
        }

        var headerFile = Path.Combine(layout.HostToolsNoticesFolder, $"notices-header-{tool.FileName}.txt");
        if (!File.Exists(headerFile))
        {
            throw new FileNotFoundException($"The notices header '{headerFile}' does not exist.", headerFile);
        }

        var isDx = string.Equals(package, LicenseCheck.DioxusCliPackage, StringComparison.Ordinal);
        var perTarget = isDx ? audits.DioxusCliCompiledIn : audits.WasmBindgenCliCompiledIn;
        var findings = perTarget.Values
            .SelectMany(r => r.Findings)
            .GroupBy(f => f.PackageId, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(f => f.CrateName, StringComparer.Ordinal)
            .ThenBy(f => f.Version, StringComparer.Ordinal)
            .ToList();
        var first = perTarget.Values.FirstOrDefault();
        var lockFile = CargoLockFile.Load(Path.Combine(isDx ? layout.DioxusCli : layout.WasmBindgenCli, CargoLockFile.FileName));
        var source = isDx ? "dioxus-cli" : "wasm-bindgen-cli";
        var section = new ThirdPartyNoticesSection
        {
            Title = $"Rust crates compiled into {tool.Program}",
            Introduction = $"Every crate cargo compiles into {tool.Program} for at least one of the five platforms it is built for\n"
                + $"({string.Join(", ", LicenseCheck.DistributedToolTargets)}).\n"
                + "A few are compiled in on some platforms only (operating-system bindings). Their source is in\n"
                + $"the tool vendor folder of the CodeBrix.WebPlatform repository ({layout.ToolingVendorRelative}), unmodified;\n"
                + $"the {source} source itself is beside it ({layout.ToolSourcesRelative}/{source}).",
            Report = new LicenseAuditReport
            {
                Findings = findings,
                Policy = first?.Policy ?? new LicensePolicy(),
                Scope = LicenseAuditScope.LinkedClosure,
            },
            LockFile = lockFile,
        };
        var header = File.ReadAllText(headerFile, Encoding.UTF8);
        return ThirdPartyNoticesGenerator.Generate(new[] { section }, new ThirdPartyNoticesOptions { Header = header });
    }

    /// <summary>
    /// Builds the platform crates' notices text from the license audits: Part 1, the crates
    /// compiled into a platform application's WebAssembly file; Part 2, the rest of the
    /// platform's vendored crates.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="audits">The license audits.</param>
    /// <exception cref="FileNotFoundException">The notices header file is missing.</exception>
    public static string Generate(RepositoryLayout layout, LicenseAudits audits)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(audits);
        var lockFile = CargoLockFile.Load(layout.PlatformCrateSetLock);
        var linkedIds = new HashSet<string>(audits.Application.Findings.Select(f => f.PackageId), StringComparer.Ordinal);
        var vendoredOnly = audits.Vendored.Findings.Where(f => !linkedIds.Contains(f.PackageId)).ToList();
        var sections = new[]
        {
            new ThirdPartyNoticesSection
            {
                Title = "PART 1 - the platform's crates compiled into a platform application",
                Introduction = "These crates are linked into the WebAssembly file of a platform application that uses the\n"
                    + "platform's crates (the shared crate's dependencies and the Dioxus features the platform offers;\n"
                    + "an application compiles in those it uses). Their source is in the archive platform-crates.zip\n"
                    + "of the CodeBrix.WebPlatform.FrontendBuilder package, and in the repository at\n"
                    + $"{layout.Relative(layout.PlatformVendor)}, unmodified.",
                Report = audits.Application,
                LockFile = lockFile,
            },
            new ThirdPartyNoticesSection
            {
                Title = "PART 2 - the platform's crates that are carried but not compiled into an application",
                Introduction = "These crates are named by the platform's Cargo.lock (for other platforms, for build scripts and\n"
                    + "procedural macros, or for features that are switched off), so `cargo vendor` takes them too\n"
                    + "and the package carries them, unmodified. None of their code is in an application's\n"
                    + "WebAssembly file.",
                Report = new LicenseAuditReport { Findings = vendoredOnly, Policy = audits.Vendored.Policy, Scope = audits.Vendored.Scope },
                LockFile = lockFile,
            },
        };
        return ThirdPartyNoticesGenerator.Generate(sections, new ThirdPartyNoticesOptions { Header = ReadHeader(layout.PlatformNoticesHeaderFile) });
    }

    /// <summary>
    /// Builds the tool sources' notices text: every crate vendored to build the two tools.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="audits">The license audits.</param>
    /// <exception cref="FileNotFoundException">The notices header file is missing.</exception>
    public static string GenerateToolSources(RepositoryLayout layout, LicenseAudits audits)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(audits);
        var toolingLock = new CargoLockFile
        {
            Packages = CargoLockFile.Load(Path.Combine(layout.DioxusCli, CargoLockFile.FileName)).Packages
                .Concat(CargoLockFile.Load(Path.Combine(layout.WasmBindgenCli, CargoLockFile.FileName)).Packages)
                .ToList(),
        };
        var tooling = audits.DioxusCli.Findings.Concat(audits.WasmBindgenCli.Findings)
            .GroupBy(f => f.CrateName + "\n" + f.Version, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        var section = new ThirdPartyNoticesSection
        {
            Title = "Rust crates vendored for the build tools",
            Introduction = "These crates build the two tools whose source is in " + layout.ToolSourcesRelative + ": the Dioxus\n"
                + "CLI (dx) and the wasm-bindgen CLI. Their source is in " + layout.ToolingVendorRelative + ", unmodified.\n"
                + "This part includes crates under the Mozilla Public License 2.0, which is accepted in the build\n"
                + "tools' tree; thirteen of them build the distributed dx (ten are compiled into it, three are\n"
                + "procedural macros) and are accepted for that by name. What is compiled into each distributed\n"
                + "program is recorded in THIRD-PARTY-NOTICES-Dioxus.txt and THIRD-PARTY-NOTICES-WasmBindgen.txt\n"
                + "beside this file, which travel with the programs.",
            Report = new LicenseAuditReport { Findings = tooling, Policy = audits.DioxusCli.Policy, Scope = audits.DioxusCli.Scope },
            LockFile = toolingLock,
        };
        return ThirdPartyNoticesGenerator.Generate(new[] { section }, new ThirdPartyNoticesOptions { Header = ReadHeader(layout.ToolSourcesNoticesHeaderFile) });
    }

    private static string ReadHeader(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The notices header '{path}' does not exist.", path);
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>
    /// Compares the generated text with the committed notices file.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="audits">The license audits.</param>
    public static CheckResult Check(RepositoryLayout layout, LicenseAudits audits)
    {
        const string name = "Notices: the platform crates', the distributed tools' and the tool sources' notices are up to date";
        var problems = new List<string>();
        foreach (var (path, expected) in Expected(layout, audits))
        {
            var shown = layout.Relative(path);
            if (!File.Exists(path))
            {
                problems.Add($"{shown} does not exist; run `LibraryChecker notices --write`");
                continue;
            }

            var actual = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                problems.Add($"{shown} differs from what the vendored crates produce; run `LibraryChecker notices --write` and review the change");
            }
        }

        return new CheckResult(name, problems);
    }

    private static IEnumerable<(string Path, string Text)> Expected(RepositoryLayout layout, LicenseAudits audits)
    {
        yield return (layout.PlatformNoticesFile, Generate(layout, audits));
        yield return (layout.ToolSourcesNoticesFile, GenerateToolSources(layout, audits));
        foreach (var tool in HostTools)
        {
            yield return (HostToolNoticesFile(layout, tool.FileName), GenerateHostToolNotices(layout, audits, tool.Package));
        }
    }

    /// <summary>
    /// Writes the notices files (line feeds, UTF-8 without a byte-order mark).
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="audits">The license audits.</param>
    /// <returns>True when a file's content changed.</returns>
    public static bool Write(RepositoryLayout layout, LicenseAudits audits)
    {
        var changed = false;
        foreach (var (path, text) in Expected(layout, audits).ToList())
        {
            var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            if (string.Equals(existing, text, StringComparison.Ordinal))
            {
                continue;
            }

            File.WriteAllText(path, text, new UTF8Encoding(false));
            changed = true;
        }

        return changed;
    }
}
