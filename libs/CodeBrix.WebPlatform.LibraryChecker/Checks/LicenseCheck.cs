using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.RustTools;
using CodeBrix.RustTools.Licensing;
using CodeBrix.RustTools.Projects;
using CodeBrix.RustTools.Vendoring;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// The license gate: audits the platform's crates that are compiled into a platform
/// application against the strictest tier, all of the platform's vendored crates against the
/// middle tier, and the build tools' vendored
/// trees against the widest tier - each time also scanning the crate folders for license files
/// the crates do not declare. Because the two tools are DISTRIBUTED as programs, it also audits,
/// for each of the five platforms they are built for, the crates compiled into them: those must
/// satisfy the strictest tier, except where the allowlist accepts a license for named crates
/// only. It also confirms that every vendored crate was audited.
/// </summary>
public static class LicenseCheck
{
    /// <summary>The platform a platform application is compiled for.</summary>
    public const string ApplicationTarget = "wasm32-unknown-unknown";

    /// <summary>The platforms the distributed tools are built for (linux-x64, linux-arm64, win-x64, osx-arm64, osx-x64).</summary>
    public static readonly IReadOnlyList<string> DistributedToolTargets = new[]
    {
        "x86_64-unknown-linux-gnu", "aarch64-unknown-linux-gnu", "x86_64-pc-windows-msvc", "aarch64-apple-darwin", "x86_64-apple-darwin",
    };

    /// <summary>The Dioxus CLI's package name, as the allowlist's named-crate entries name it.</summary>
    public const string DioxusCliPackage = "dioxus-cli";

    /// <summary>The wasm-bindgen CLI's package name, as the allowlist's named-crate entries name it.</summary>
    public const string WasmBindgenCliPackage = "wasm-bindgen-cli";

    /// <summary>The cargo features the distributed <c>dx</c> is built with (DX_FEATURES in tools/dx-native-tools/linux/pins.env).</summary>
    public static readonly IReadOnlyList<string> DioxusCliFeatures = new[] { "no-downloads", "disable-telemetry" };

    /// <summary>
    /// Runs the four audits. cargo is run offline against the vendored folders.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="allowlist">The approved licenses.</param>
    /// <param name="cancellationToken">Cancels the cargo runs.</param>
    /// <returns>One result per audit, a coverage result, and the audit reports.</returns>
    public static async Task<(IReadOnlyList<CheckResult> Results, LicenseAudits Audits)> RunAsync(RepositoryLayout layout, LicenseAllowlist allowlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(allowlist);
        var workbench = new RustWorkbench();
        var crateSet = workbench.OpenProject(layout.PlatformCrateSet);
        var dioxusCli = workbench.OpenProject(layout.DioxusCli);
        var wasmBindgenCli = workbench.OpenProject(layout.WasmBindgenCli);

        var application = await workbench.AuditLicensesAsync(
            crateSet,
            allowlist.BuildApplicationPolicy(),
            new CargoMetadataOptions { Frozen = true, FilterPlatform = ApplicationTarget },
            LicenseAuditScope.LinkedClosure,
            cancellationToken).ConfigureAwait(false);
        var vendored = await workbench.AuditLicensesAsync(
            crateSet,
            allowlist.BuildVendoredPolicy(),
            new CargoMetadataOptions { Frozen = true, AllFeatures = true },
            cancellationToken).ConfigureAwait(false);
        var dx = await workbench.AuditLicensesAsync(
            dioxusCli,
            allowlist.BuildToolingPolicy(),
            new CargoMetadataOptions { Frozen = true, AllFeatures = true },
            cancellationToken).ConfigureAwait(false);
        var bindgen = await workbench.AuditLicensesAsync(
            wasmBindgenCli,
            allowlist.BuildToolingPolicy(),
            new CargoMetadataOptions { Frozen = true, AllFeatures = true },
            cancellationToken).ConfigureAwait(false);

        var dxCompiledIn = await AuditCompiledInAsync(workbench, dioxusCli, DioxusCliPackage, DioxusCliFeatures, allowlist, cancellationToken).ConfigureAwait(false);
        var bindgenCompiledIn = await AuditCompiledInAsync(workbench, wasmBindgenCli, WasmBindgenCliPackage, Array.Empty<string>(), allowlist, cancellationToken).ConfigureAwait(false);

        var results = new List<CheckResult>
        {
            ToResult("Licenses: the platform's crates compiled into a platform application (strictest tier)", application),
            ToResult("Licenses: the platform's vendored crates (shipped in the FrontendBuilder package)", vendored),
            ToToolResult("Licenses: build tools - Dioxus CLI (vendored tree; compiled into the distributed dx)", dx, DioxusCliPackage, dxCompiledIn, allowlist),
            ToToolResult("Licenses: build tools - wasm-bindgen CLI (vendored tree; compiled into the distributed wasm-bindgen)", bindgen, WasmBindgenCliPackage, bindgenCompiledIn, allowlist),
            Coverage("Licenses: every vendored platform crate was audited", VendorFolder.Load(layout.PlatformVendor), vendored),
            Coverage("Licenses: every vendored build-tool crate was audited", VendorFolder.Load(layout.ToolingVendor), dx, bindgen),
        };
        return (results, new LicenseAudits
        {
            Application = application,
            Vendored = vendored,
            DioxusCli = dx,
            WasmBindgenCli = bindgen,
            DioxusCliCompiledIn = dxCompiledIn,
            WasmBindgenCliCompiledIn = bindgenCompiledIn,
        });
    }

    private static async Task<IReadOnlyDictionary<string, LicenseAuditReport>> AuditCompiledInAsync(
        RustWorkbench workbench,
        CargoProject project,
        string tool,
        IReadOnlyList<string> features,
        LicenseAllowlist allowlist,
        CancellationToken cancellationToken)
    {
        var reports = new Dictionary<string, LicenseAuditReport>(StringComparer.Ordinal);
        foreach (var target in DistributedToolTargets)
        {
            var options = new CargoMetadataOptions { Frozen = true, FilterPlatform = target };
            foreach (var feature in features)
            {
                options.Features.Add(feature);
            }

            reports[target] = await workbench.AuditLicensesAsync(
                project,
                allowlist.BuildDistributedToolPolicy(tool),
                options,
                LicenseAuditScope.LinkedClosure,
                cancellationToken).ConfigureAwait(false);
        }

        return reports;
    }

    /// <summary>
    /// The result for one build tool: its vendored tree (the widest tier) and, per platform, the
    /// crates compiled into the distributed program (<see cref="CompiledInProblems"/>).
    /// </summary>
    /// <param name="name">The check's name.</param>
    /// <param name="tree">The audit of the tool's whole vendored tree.</param>
    /// <param name="tool">The tool's package name.</param>
    /// <param name="compiledIn">The per-platform audits of what is compiled into the program.</param>
    /// <param name="allowlist">The approved licenses.</param>
    public static CheckResult ToToolResult(string name, LicenseAuditReport tree, string tool, IReadOnlyDictionary<string, LicenseAuditReport> compiledIn, LicenseAllowlist allowlist)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(compiledIn);
        ArgumentNullException.ThrowIfNull(allowlist);
        var treeResult = ToResult(name, tree);
        var problems = treeResult.Problems.ToList();
        problems.AddRange(CompiledInProblems(tool, compiledIn, allowlist, tree));
        var linked = compiledIn.Values.SelectMany(r => r.Findings).Select(f => f.CrateName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var crates = compiledIn.Values.SelectMany(r => r.Findings).Where(f => !f.IsWorkspaceMember).Select(f => f.PackageId).Distinct(StringComparer.Ordinal).Count();
        var namedNames = allowlist.NamedCrateLicenses.Where(n => n.AppliesToTool(tool)).SelectMany(n => n.Crates).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var notes = treeResult.Notes.Concat(new[]
        {
            $"compiled into the distributed program on {compiledIn.Count} platforms: {crates} crates, {namedNames.Count(linked.Contains)} of them accepted by name",
        }).ToList();
        var buildOnly = namedNames.Where(c => !linked.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (buildOnly.Count > 0)
        {
            notes.Add("accepted by name but not linked into the program (procedural macros, run while it is compiled): " + string.Join(", ", buildOnly));
        }

        return new CheckResult(name, problems, notes);
    }

    /// <summary>
    /// Judges the crates compiled into a distributed tool. Every crate must satisfy the strictest
    /// tier (with the distributed-tool exceptions); a crate the allowlist names may also rely on
    /// the license it is named for, and nothing else. A crate that is not named may not ship a
    /// license file of a license accepted only for named crates. A named crate that is not in the
    /// tool's tree at all is reported too, so the list stays exact. (A named crate may be absent
    /// from the linked crates when it is a procedural macro: it runs while the tool is compiled
    /// and generates code that is compiled in.)
    /// </summary>
    /// <param name="tool">The tool's package name.</param>
    /// <param name="compiledIn">The per-platform audits (built with <see cref="LicenseAllowlist.BuildDistributedToolPolicy"/>).</param>
    /// <param name="allowlist">The approved licenses.</param>
    /// <param name="tree">The audit of the tool's whole vendored tree, or <c>null</c> to require every named crate to be linked.</param>
    /// <returns>One line per problem; empty when everything passes.</returns>
    public static IReadOnlyList<string> CompiledInProblems(string tool, IReadOnlyDictionary<string, LicenseAuditReport> compiledIn, LicenseAllowlist allowlist, LicenseAuditReport tree = null)
    {
        ArgumentNullException.ThrowIfNull(compiledIn);
        ArgumentNullException.ThrowIfNull(allowlist);
        var problems = new List<string>();
        var basePolicy = allowlist.BuildDistributedToolBasePolicy();
        var namedOnly = allowlist.NamedCrateLicenses.Where(n => n.AppliesToTool(tool)).Select(n => n.License).ToHashSet();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (target, report) in compiledIn)
        {
            foreach (var finding in report.Findings)
            {
                seen.Add(finding.CrateName);
                if (finding.IsWorkspaceMember)
                {
                    continue;
                }

                var where = $"compiled into {tool} for {target}: {finding.CrateName} {finding.Version} ({finding.DeclaredExpression ?? "no license declared"})";
                if (finding.IsBlocking)
                {
                    problems.Add($"{where}: {finding.Reason}");
                    continue;
                }

                var isNamed = allowlist.NamedCrateLicenses.Any(n => n.AppliesTo(tool, finding.CrateName));
                if (finding.ParsedExpression != null && !finding.ParsedExpression.Evaluate(basePolicy).IsApproved)
                {
                    if (!isNamed)
                    {
                        problems.Add($"{where}: relies on a license the allowlist accepts only for named crates, and this crate is not named");
                    }
                    else if (!finding.ParsedExpression.Evaluate(allowlist.BuildDistributedToolPolicyFor(tool, finding.CrateName)).IsApproved)
                    {
                        problems.Add($"{where}: relies on a license other than the one it is named for");
                    }
                }

                if (!isNamed)
                {
                    var bundled = finding.BundledLicenses
                        .Where(b => b.UndeclaredLicenses.Any(l => namedOnly.Contains(l)))
                        .Select(b => b.File.RelativePath)
                        .ToList();
                    if (bundled.Count > 0)
                    {
                        problems.Add($"{where}: ships {string.Join(", ", bundled)} under a license accepted only for named crates");
                    }
                }
            }
        }

        if (tree != null)
        {
            seen.UnionWith(tree.Findings.Select(f => f.CrateName));
        }

        foreach (var entry in allowlist.NamedCrateLicenses.Where(n => n.AppliesToTool(tool)))
        {
            foreach (var crate in entry.Crates.Where(c => !seen.Contains(c)))
            {
                problems.Add($"{crate} is accepted by name for {OpenSourceLicenseInfo.ToSpdxId(entry.License)} in {tool}, but {tool} is not built from it; remove it from the allowlist");
            }
        }

        return problems;
    }

    /// <summary>
    /// Turns an audit report into a check result: one problem per blocking crate.
    /// </summary>
    /// <param name="name">The check's name.</param>
    /// <param name="report">The audit report.</param>
    public static CheckResult ToResult(string name, LicenseAuditReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var problems = report.BlockingFindings
            .Select(f => $"{f.CrateName} {f.Version} ({f.DeclaredExpression ?? "no license declared"}): {f.Reason}")
            .ToList();
        var scanned = report.Findings.Count(f => f.BundledLicensesScanned);
        var notes = new[]
        {
            $"{report.Findings.Count} crates: {report.ApprovedCount} approved, {report.ExemptCount} exempt (our own), {report.RejectedCount} rejected, {report.UnresolvedCount} unresolved; {scanned} crate folders scanned for license files",
        };
        return new CheckResult(name, problems, notes);
    }

    /// <summary>
    /// Confirms that every crate in a vendor folder has a finding in at least one of the audits,
    /// so nothing sits in the repository without having been judged.
    /// </summary>
    /// <param name="name">The check's name.</param>
    /// <param name="vendor">The vendor folder.</param>
    /// <param name="reports">The audits that should cover it.</param>
    public static CheckResult Coverage(string name, VendorFolder vendor, params LicenseAuditReport[] reports)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        ArgumentNullException.ThrowIfNull(reports);
        var audited = new HashSet<string>(reports.SelectMany(r => r.Findings).Select(f => f.CrateName + "\n" + f.Version), StringComparer.Ordinal);
        var problems = vendor.Crates
            .Where(c => !audited.Contains(c.Name + "\n" + c.Version))
            .Select(c => $"{c.Name} {c.Version} is vendored ({c.DirectoryName}) but no license audit covered it")
            .ToList();
        return new CheckResult(name, problems, new[] { $"{vendor.Crates.Count} vendored crates" });
    }
}
