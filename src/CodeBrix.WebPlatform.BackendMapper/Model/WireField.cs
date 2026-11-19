namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One property of a contract class.</summary>
public sealed class WireField
{
    internal WireField(string clrName, string jsonName, WireType type)
    {
        ClrName = clrName;
        JsonName = jsonName;
        Type = type;
    }

    /// <summary>The .NET property name.</summary>
    public string ClrName { get; }

    /// <summary>The name the property has in JSON.</summary>
    public string JsonName { get; }

    /// <summary>The property's type.</summary>
    public WireType Type { get; }
}
