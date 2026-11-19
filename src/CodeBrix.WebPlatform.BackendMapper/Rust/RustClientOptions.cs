namespace CodeBrix.WebPlatform.BackendMapper.Rust;

/// <summary>How <see cref="RustClientGenerator"/> writes the client code.</summary>
public sealed class RustClientOptions
{
    /// <summary>
    /// The Rust path of the platform's shared crate, which the generated REST functions call.
    /// </summary>
    public string SharedCratePath { get; set; } = "codebrix_webplatform";

    /// <summary>
    /// The Rust path of the SignalR client module, which the generated hub types call. The
    /// default is the client in the platform's shared crate. Only an application that carries a
    /// SignalR client of its own, with the same API, names it here, for example
    /// <c>crate::signalr</c>.
    /// </summary>
    public string SignalRModulePath { get; set; } = "codebrix_webplatform::signalr";

    /// <summary>
    /// An endpoint with more query-string values than this takes them as one generated struct
    /// instead of as separate arguments.
    /// </summary>
    public int MaxSeparateQueryArguments { get; set; } = 2;

    /// <summary>The templates the files are written from.</summary>
    public RustTemplates Templates { get; set; } = new RustTemplates();
}
