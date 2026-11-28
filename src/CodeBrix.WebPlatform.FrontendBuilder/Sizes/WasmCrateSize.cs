namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// How much of a WebAssembly file's code belongs to one Rust crate (<see cref="WasmFileSize.Crates"/>,
/// only for a build that keeps the function names, <see cref="PlatformFrontendOptions.KeepFunctionNames"/>).
/// </summary>
public sealed class WasmCrateSize
{
    /// <summary>The name of the bucket of functions the file names without a mangled Rust name (<c>memcpy</c>, <c>__wbindgen_malloc</c>).</summary>
    public const string OtherNames = "(other names)";

    /// <summary>The name of the bucket of functions the file gives no name.</summary>
    public const string NoName = "(no name)";

    internal WasmCrateSize(string crate, long codeBytes, int functionCount)
    {
        Crate = crate;
        CodeBytes = codeBytes;
        FunctionCount = functionCount;
    }

    /// <summary>
    /// The crate's name as Rust source writes it (<c>dioxus_core</c>), or <see cref="OtherNames"/> /
    /// <see cref="NoName"/>. A function counts for the crate that DEFINES it: generic code for the
    /// crate it is written in, whatever it was instantiated for.
    /// </summary>
    public string Crate { get; }

    /// <summary>The size of the crate's function bodies in the file, in bytes.</summary>
    public long CodeBytes { get; }

    /// <summary>How many of the file's functions belong to the crate.</summary>
    public int FunctionCount { get; }

    /// <summary>A short text fit to show.</summary>
    /// <returns>For example <c>dioxus_core 120345</c>.</returns>
    public override string ToString() => Crate + " " + CodeBytes;
}
