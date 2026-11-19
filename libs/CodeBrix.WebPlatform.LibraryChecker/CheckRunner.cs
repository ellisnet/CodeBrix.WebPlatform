using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.LibraryChecker.Checks;

namespace CodeBrix.WebPlatform.LibraryChecker;

/// <summary>
/// Runs the checks that guard what the repository holds outside <c>samples/</c>: the platform's
/// crates, the tool sources and their crates, the patches, the licences, the notices and the
/// no-Node rule. Used by the LibraryChecker command line and its tests.
/// </summary>
public static class CheckRunner
{
    /// <summary>The names of the check groups, in the order <c>all</c> runs them.</summary>
    public static readonly IReadOnlyList<string> Groups = new[] { "no-node", "vendor", "patches", "licenses", "notices" };

    /// <summary>
    /// Runs one group of checks, or all of them.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="group">One of <see cref="Groups"/>, or <c>all</c>.</param>
    /// <param name="writeNotices">For the notices group: write the generated notices files instead
    /// of comparing with them.</param>
    /// <param name="cancellationToken">Cancels the cargo runs.</param>
    /// <returns>Every check's result, in run order.</returns>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not a known group.</exception>
    public static async Task<IReadOnlyList<CheckResult>> RunAsync(RepositoryLayout layout, string group = "all", bool writeNotices = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var all = string.Equals(group, "all", StringComparison.OrdinalIgnoreCase);
        if (!all && !Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown check '{group}'. Use one of: all, {string.Join(", ", Groups)}.", nameof(group));
        }

        bool Wanted(string name) => all || string.Equals(group, name, StringComparison.OrdinalIgnoreCase);
        var results = new List<CheckResult>();
        if (Wanted("no-node"))
        {
            results.Add(NoNodeCheck.Run(layout));
        }

        if (Wanted("vendor"))
        {
            results.Add(VendorCheck.Platform(layout));
            results.Add(VendorCheck.SharedCrate(layout));
            results.Add(VendorCheck.Tooling(layout));
            results.Add(NoLibgit2SourceCheck.Run(layout));
            results.Add(VendorCheck.GitVisibility(layout));
        }

        if (Wanted("patches"))
        {
            results.AddRange(PatchCheck.Run(layout));
        }

        if (Wanted("licenses") || Wanted("notices"))
        {
            var allowlist = LicenseAllowlist.Load(layout.AllowlistFile);
            var (licenseResults, audits) = await LicenseCheck.RunAsync(layout, allowlist, cancellationToken).ConfigureAwait(false);
            if (Wanted("licenses"))
            {
                results.AddRange(licenseResults);
            }

            if (Wanted("notices"))
            {
                if (writeNotices)
                {
                    var changed = NoticesCheck.Write(layout, audits);
                    results.Add(new CheckResult("Notices: the generated notices files written", Array.Empty<string>(), new[] { changed ? "a file changed" : "every file was already up to date" }));
                }
                else
                {
                    results.Add(NoticesCheck.Check(layout, audits));
                }
            }
        }

        return results;
    }
}
