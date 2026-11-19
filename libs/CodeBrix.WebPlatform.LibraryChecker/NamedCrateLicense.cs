using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.RustTools.Licensing;

namespace CodeBrix.WebPlatform.LibraryChecker;

/// <summary>
/// A license the repository owner accepts in the code compiled into the distributed tools for
/// the named crates only - never as a tier. Read from
/// <c>compiledIntoTheDistributedTools.namedCrates</c> in the license allowlist.
/// </summary>
public sealed class NamedCrateLicense
{
    /// <summary>The license that is accepted for the named crates.</summary>
    public OpenSourceLicense License { get; init; }

    /// <summary>The tool packages (for example <c>dioxus-cli</c>) the acceptance applies to; empty means every tool.</summary>
    public IReadOnlyList<string> Tools { get; init; } = Array.Empty<string>();

    /// <summary>The crate names the license is accepted for.</summary>
    public IReadOnlyList<string> Crates { get; init; } = Array.Empty<string>();

    /// <summary>Why the owner accepted it.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// True when this acceptance covers a tool.
    /// </summary>
    /// <param name="tool">The tool's package name.</param>
    public bool AppliesToTool(string tool)
        => Tools.Count == 0 || Tools.Contains(tool, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when this acceptance covers a crate compiled into a tool.
    /// </summary>
    /// <param name="tool">The tool's package name.</param>
    /// <param name="crateName">The crate's name.</param>
    public bool AppliesTo(string tool, string crateName)
        => AppliesToTool(tool) && Crates.Contains(crateName, StringComparer.OrdinalIgnoreCase);
}
