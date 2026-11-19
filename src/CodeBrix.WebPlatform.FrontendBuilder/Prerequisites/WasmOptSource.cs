namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>Where the <c>wasm-opt</c> that shrinks a frontend's WebAssembly file comes from.</summary>
public enum WasmOptSource
{
    /// <summary>The CodeBrix.WebPlatform.WasmShrink package: nothing is installed on the machine.</summary>
    Package,

    /// <summary>A <c>wasm-opt</c> installed on PATH, because the package does not include this platform yet.</summary>
    Installed,

    /// <summary>Neither: a build that must be shrunk cannot be made on this machine.</summary>
    Missing,
}
