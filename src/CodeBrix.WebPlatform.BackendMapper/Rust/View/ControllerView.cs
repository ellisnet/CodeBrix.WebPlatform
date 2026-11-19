using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A controller as the api template sees it: one Rust module.</summary>
internal sealed class ControllerView
{
    public string Name { get; set; }

    public string ClrName { get; set; }

    public string Module { get; set; }

    public List<QueryStructView> QueryStructs { get; } = new();

    public List<FunctionView> Functions { get; } = new();
}
