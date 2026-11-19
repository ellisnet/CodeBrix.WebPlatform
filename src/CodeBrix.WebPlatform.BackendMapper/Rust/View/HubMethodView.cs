namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A hub method the frontend can call.</summary>
internal sealed class HubMethodView
{
    public string Name { get; set; }

    public string WireName { get; set; }

    public bool IsStream { get; set; }

    public string Parameters { get; set; }

    public string Arguments { get; set; }

    public string ResultType { get; set; }
}
