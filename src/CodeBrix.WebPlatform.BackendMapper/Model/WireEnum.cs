using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One of the application's enums.</summary>
public sealed class WireEnum
{
    internal WireEnum(string name, string clrName, bool asText)
    {
        Name = name;
        ClrName = clrName;
        AsText = asText;
    }

    /// <summary>The name the enum has in the generated code.</summary>
    public string Name { get; }

    /// <summary>The full name of the .NET enum.</summary>
    public string ClrName { get; }

    /// <summary>
    /// True when the enum is written to JSON as the member's name, which is the case when the
    /// enum type carries <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c>. Otherwise it is
    /// written as the member's number.
    /// </summary>
    public bool AsText { get; }

    /// <summary>The members, in declaration order.</summary>
    public IReadOnlyList<WireEnumMember> Members => MemberList;

    internal List<WireEnumMember> MemberList { get; } = new();
}
