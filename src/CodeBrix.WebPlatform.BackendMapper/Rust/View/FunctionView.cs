namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>An endpoint as the api template sees it: one Rust function.</summary>
internal sealed class FunctionView
{
    public string Name { get; set; }

    public string Doc { get; set; }

    public string Parameters { get; set; }

    public bool IsFile { get; set; }

    public bool HasResponse { get; set; }

    public string ReturnType { get; set; }

    public string Method { get; set; }

    public string Auth { get; set; }

    /// <summary>A statement that builds <c>query_string</c>, or empty.</summary>
    public string QueryStatement { get; set; }

    /// <summary>An expression for the path with its query string: a string literal, or a <c>format!</c> call.</summary>
    public string Path { get; set; }

    /// <summary><c>None</c> or <c>Some(client::body(body)?)</c>.</summary>
    public string Body { get; set; }
}
