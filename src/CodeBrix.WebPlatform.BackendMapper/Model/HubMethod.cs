using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One method of a hub that the frontend can call.</summary>
public sealed class HubMethod
{
    internal HubMethod(string name, HubMethodKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>The method's name, which is also its name on the wire.</summary>
    public string Name { get; }

    /// <summary>Whether it answers once or with a stream.</summary>
    public HubMethodKind Kind { get; }

    /// <summary>The arguments, in order.</summary>
    public IReadOnlyList<HubArgument> Arguments => ArgumentList;

    /// <summary>The type of the single answer, or of each streamed item; null when the method
    /// answers with nothing.</summary>
    public WireType Result { get; internal set; }

    internal List<HubArgument> ArgumentList { get; } = new();
}
