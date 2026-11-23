namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>The things <see cref="PlatformFrontend.CheckPrerequisitesAsync"/> checks.</summary>
public enum PrerequisiteKind
{
    /// <summary>Rust (cargo and rustc) at <see cref="FrontendRequirements.MinimumRustVersion"/> or later,
    /// with the <see cref="FrontendRequirements.WebAssemblyTarget"/> target.</summary>
    Rust,

    /// <summary>The standalone Tailwind program from the Styling package beside the build program
    /// (see <see cref="Tools.PackagedTailwind"/>), started once with <c>--help</c> to prove it runs.</summary>
    Tailwind,

    /// <summary>A <c>wasm-opt</c> to shrink release builds with (see <see cref="WasmOptSource"/>).</summary>
    WasmOpt,

    /// <summary>The <c>dx</c> and <c>wasm-bindgen</c> programs (see <see cref="Tools.HostTools"/>).</summary>
    HostTools,

    /// <summary>
    /// The libgit2 library dx needs to start, from the CodeBrix.LibGit2.MitLicenseForever package
    /// beside the build program (see <see cref="PlatformFrontend.Libgit2Directory"/>).
    /// </summary>
    Libgit2,
}
