using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A contract class as the models template sees it.</summary>
internal sealed class StructView
{
    public string Name { get; set; }

    public string ClrName { get; set; }

    public string Derives { get; set; }

    public List<FieldView> Fields { get; } = new();
}
