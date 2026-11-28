using System.Collections.Generic;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>What <see cref="WasmModuleReader"/> read from one WebAssembly file.</summary>
internal sealed class WasmModuleInfo
{
    /// <summary>The size of the code section's content, in bytes (0 without one).</summary>
    public long CodeBytes { get; set; }

    /// <summary>The size of the data section's content, in bytes (0 without one).</summary>
    public long DataBytes { get; set; }

    /// <summary>How many functions are imported; the file's own functions are numbered after them.</summary>
    public int ImportedFunctionCount { get; set; }

    /// <summary>The size of each function body of the code section, in order.</summary>
    public List<int> BodySizes { get; } = new List<int>();

    /// <summary>Whether the file has a <c>name</c> custom section.</summary>
    public bool HasNameSection { get; set; }

    /// <summary>The function names of the <c>name</c> section, by function index (imports included).</summary>
    public Dictionary<int, string> FunctionNames { get; } = new Dictionary<int, string>();
}
