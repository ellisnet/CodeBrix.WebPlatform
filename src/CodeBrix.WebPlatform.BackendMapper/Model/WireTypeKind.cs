namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>The shape of a value that crosses the wire.</summary>
public enum WireTypeKind
{
    /// <summary>A single value with a Rust type from the type map (a number, text, a date).</summary>
    Scalar,

    /// <summary>A sequence of values (an array or a list).</summary>
    List,

    /// <summary>Values keyed by text (a dictionary with string keys).</summary>
    Map,

    /// <summary>An object: one of the application's contract classes.</summary>
    Struct,

    /// <summary>One of the application's enums.</summary>
    Enum,
}
