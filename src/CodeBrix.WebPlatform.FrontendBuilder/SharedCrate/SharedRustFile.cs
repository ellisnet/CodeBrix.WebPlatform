namespace CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

/// <summary>One file of the shared Rust crate.</summary>
public sealed class SharedRustFile
{
    internal SharedRustFile(string relativePath, byte[] content)
    {
        RelativePath = relativePath;
        Content = content;
    }

    /// <summary>
    /// The file's path inside the crate folder, with <c>/</c> separators, such as
    /// <c>src/lib.rs</c>.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>The file's bytes, exactly as they are written out.</summary>
    public byte[] Content { get; }
}
