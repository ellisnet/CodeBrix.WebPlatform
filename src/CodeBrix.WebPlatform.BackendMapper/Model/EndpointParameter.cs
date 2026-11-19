namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One value a REST endpoint takes.</summary>
public sealed class EndpointParameter
{
    internal EndpointParameter(string name, string wireName, ParameterSource source, WireType type)
    {
        Name = name;
        WireName = wireName;
        Source = source;
        Type = type;
    }

    /// <summary>The .NET parameter (or, for a query object, property) name.</summary>
    public string Name { get; }

    /// <summary>The name on the wire: the route token or the query-string key. Null for the body.</summary>
    public string WireName { get; }

    /// <summary>Where the value travels.</summary>
    public ParameterSource Source { get; }

    /// <summary>The value's type.</summary>
    public WireType Type { get; }
}
