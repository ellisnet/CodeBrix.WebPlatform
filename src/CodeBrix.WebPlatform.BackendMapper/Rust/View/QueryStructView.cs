using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>The struct that carries an endpoint's query-string values when there are many.</summary>
internal sealed class QueryStructView
{
    public string Name { get; set; }

    public string Doc { get; set; }

    public List<FieldView> Fields { get; } = new();
}
