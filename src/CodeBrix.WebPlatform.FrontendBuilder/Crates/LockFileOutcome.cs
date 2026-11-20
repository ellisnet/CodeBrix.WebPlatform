using System;
using System.Collections.Generic;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>What MaterializeAsync found and did about one frontend's Cargo.lock.</summary>
internal sealed class LockFileOutcome
{
    /// <summary>The frontend has no lock file (not possible after the starting one is written).</summary>
    internal static readonly LockFileOutcome None = new LockFileOutcome(false);

    internal LockFileOutcome(bool marked)
    {
        Marked = marked;
    }

    /// <summary>The lock file carries the marker line.</summary>
    internal bool Marked { get; }

    /// <summary>The entries brought in step (marked lock file only).</summary>
    internal IReadOnlyList<LockFileRepair> Repairs { get; init; } = Array.Empty<LockFileRepair>();

    /// <summary>The stale entries, as "name version", of a lock file without the marker.</summary>
    internal IReadOnlyList<string> Stale { get; init; } = Array.Empty<string>();

    /// <summary>The command that brings <see cref="Stale"/> in step; <c>null</c> when nothing is stale.</summary>
    internal string Command { get; init; }

    /// <summary>The warning to show; empty when there is none.</summary>
    internal string Warning { get; init; } = string.Empty;
}
