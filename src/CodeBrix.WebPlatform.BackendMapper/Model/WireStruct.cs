using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One of the application's contract classes: an object that is sent or received as JSON.</summary>
public sealed class WireStruct
{
    internal WireStruct(string name, string clrName)
    {
        Name = name;
        ClrName = clrName;
    }

    /// <summary>The name the class has in the generated code.</summary>
    public string Name { get; }

    /// <summary>The full name of the .NET class, written readably for a generic class
    /// (<c>MyApp.Contracts.Page&lt;Widget&gt;</c>).</summary>
    public string ClrName { get; }

    /// <summary>The properties that cross the wire, in declaration order.</summary>
    public IReadOnlyList<WireField> Fields => FieldList;

    internal List<WireField> FieldList { get; } = new();
}
