using System.Collections.Generic;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// The size of one WebAssembly file of a built frontend application: the main file, a piece or
/// a shared chunk (part of <see cref="FrontendSizeReport"/>).
/// </summary>
public sealed class WasmFileSize
{
    internal WasmFileSize(string path, WasmFileKind kind, long bytes, long? brotliBytes, long? gzipBytes, long codeBytes, long dataBytes, int functionCount, bool hasFunctionNames, IReadOnlyList<WasmCrateSize> crates)
    {
        Path = path;
        Kind = kind;
        Bytes = bytes;
        BrotliBytes = brotliBytes;
        GzipBytes = gzipBytes;
        CodeBytes = codeBytes;
        DataBytes = dataBytes;
        FunctionCount = functionCount;
        HasFunctionNames = hasFunctionNames;
        Crates = crates;
    }

    /// <summary>
    /// The file's path in the application's site, with forward slashes, for example
    /// <c>assets/app_bg-dxh1a2b3c.wasm</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>What the file is: the main file, a piece, a shared chunk or another file.</summary>
    public WasmFileKind Kind { get; }

    /// <summary>The size of the file, in bytes.</summary>
    public long Bytes { get; }

    /// <summary>
    /// The size of its <c>.br</c> copy in the built frontend folder, or <c>null</c> when none was
    /// written (no backend project folder, not asked for, or not smaller).
    /// </summary>
    public long? BrotliBytes { get; }

    /// <summary>
    /// The size of its <c>.gz</c> copy in the built frontend folder, or <c>null</c> when none was
    /// written.
    /// </summary>
    public long? GzipBytes { get; }

    /// <summary>The size of the file's code (the code section), in bytes.</summary>
    public long CodeBytes { get; }

    /// <summary>The size of the file's data (the data section: constants, text), in bytes.</summary>
    public long DataBytes { get; }

    /// <summary>How many functions the file defines.</summary>
    public int FunctionCount { get; }

    /// <summary>
    /// Whether the file names its functions (a <c>name</c> section): only a build with
    /// <see cref="PlatformFrontendOptions.KeepFunctionNames"/>.
    /// </summary>
    public bool HasFunctionNames { get; }

    /// <summary>
    /// The file's code by Rust crate, largest first; empty when the file does not name its
    /// functions (<see cref="HasFunctionNames"/>).
    /// </summary>
    public IReadOnlyList<WasmCrateSize> Crates { get; }

    /// <summary>A line fit to show: the path and the sizes.</summary>
    /// <returns>For example <c>assets/app_bg-dxh1a2b3c.wasm 794820 bytes, br 230112, gz 291004</c>.</returns>
    public override string ToString()
        => Path + " " + Bytes + " bytes"
            + (BrotliBytes.HasValue ? ", br " + BrotliBytes.Value : string.Empty)
            + (GzipBytes.HasValue ? ", gz " + GzipBytes.Value : string.Empty);
}
