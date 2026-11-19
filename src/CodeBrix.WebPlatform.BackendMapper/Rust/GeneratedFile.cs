namespace CodeBrix.WebPlatform.BackendMapper.Rust;

/// <summary>One file of generated Rust client code.</summary>
public sealed class GeneratedFile
{
    internal GeneratedFile(string relativePath, string content)
    {
        RelativePath = relativePath;
        Content = content;
    }

    /// <summary>The file's path inside the output folder, with <c>/</c> separators, such as <c>models.rs</c>.</summary>
    public string RelativePath { get; }

    /// <summary>The file's text, with <c>\n</c> line endings.</summary>
    public string Content { get; }
}
