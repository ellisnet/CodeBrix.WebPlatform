using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A field of a generated struct: its Rust name, its Rust type and the attribute lines above it.</summary>
internal sealed class FieldView
{
    public string Name { get; set; }

    public string Type { get; set; }

    public List<string> Attributes { get; } = new();
}
