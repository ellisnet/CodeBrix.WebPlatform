using System;
using System.Collections.Generic;
using CodeBrix.RustTools.Licensing;

namespace CodeBrix.WebPlatform.LibraryChecker.Checks;

/// <summary>
/// The license audits of the repository's vendored trees, kept so that the notices can be written
/// from the same findings the gate judged.
/// </summary>
public sealed class LicenseAudits
{
    /// <summary>The platform's crates linked into a platform application's WebAssembly file (strictest tier).</summary>
    public LicenseAuditReport Application { get; init; }

    /// <summary>Every one of the platform's vendored crates, on every platform and with every feature.</summary>
    public LicenseAuditReport Vendored { get; init; }

    /// <summary>Every crate vendored for the Dioxus CLI.</summary>
    public LicenseAuditReport DioxusCli { get; init; }

    /// <summary>Every crate vendored for the wasm-bindgen CLI.</summary>
    public LicenseAuditReport WasmBindgenCli { get; init; }

    /// <summary>The crates compiled into the distributed <c>dx</c>, one audit per platform (target triple to report).</summary>
    public IReadOnlyDictionary<string, LicenseAuditReport> DioxusCliCompiledIn { get; init; } = new Dictionary<string, LicenseAuditReport>(StringComparer.Ordinal);

    /// <summary>The crates compiled into the distributed <c>wasm-bindgen</c>, one audit per platform (target triple to report).</summary>
    public IReadOnlyDictionary<string, LicenseAuditReport> WasmBindgenCliCompiledIn { get; init; } = new Dictionary<string, LicenseAuditReport>(StringComparer.Ordinal);
}
