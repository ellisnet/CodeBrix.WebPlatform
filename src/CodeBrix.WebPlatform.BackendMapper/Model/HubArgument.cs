namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One argument of a hub method or of a message a hub sends.</summary>
public sealed class HubArgument
{
    internal HubArgument(string name, WireType type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>The .NET parameter name.</summary>
    public string Name { get; }

    /// <summary>The argument's type.</summary>
    public WireType Type { get; }
}
