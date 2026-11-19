namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One member of an enum.</summary>
public sealed class WireEnumMember
{
    internal WireEnumMember(string name, long value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>The member's name.</summary>
    public string Name { get; }

    /// <summary>The member's number.</summary>
    public long Value { get; }
}
