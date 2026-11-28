using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// The sizes of the WebAssembly files of one built frontend application
/// (<see cref="FrontendBuildResult.Sizes"/>): every file the site ships with its size and, where
/// the build wrote them, the sizes of its compressed copies; and, for a build that keeps the
/// function names (<see cref="PlatformFrontendOptions.KeepFunctionNames"/>), each file's code by
/// Rust crate. Read from the built files themselves; no program is run for it.
/// </summary>
public sealed class FrontendSizeReport
{
    internal FrontendSizeReport(IReadOnlyList<WasmFileSize> files)
    {
        Files = files;
    }

    /// <summary>
    /// The files: the main file first, then the shared chunks, then the pieces, then any other
    /// <c>.wasm</c> file, each group by path.
    /// </summary>
    public IReadOnlyList<WasmFileSize> Files { get; }

    /// <summary>The size of all the files, in bytes.</summary>
    public long Bytes => Files.Sum(f => f.Bytes);

    /// <summary>
    /// What a browser that accepts Brotli downloads for all the files, in bytes: each file's
    /// <c>.br</c> copy where one was written, the file itself otherwise.
    /// </summary>
    public long BrotliBytes => Files.Sum(f => f.BrotliBytes ?? f.Bytes);

    /// <summary>Whether the files name their functions, so that <see cref="WasmFileSize.Crates"/> is filled.</summary>
    public bool HasFunctionNames => Files.Any(f => f.HasFunctionNames);
}
