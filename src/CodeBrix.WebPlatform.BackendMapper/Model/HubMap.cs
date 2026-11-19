using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One SignalR hub: its path, the methods the frontend can call and the messages it sends.</summary>
public sealed class HubMap
{
    internal HubMap(string name, string clrName, string path)
    {
        Name = name;
        ClrName = clrName;
        Path = path;
    }

    /// <summary>The hub class's name, such as <c>ClockHub</c>.</summary>
    public string Name { get; }

    /// <summary>The full name of the .NET class.</summary>
    public string ClrName { get; }

    /// <summary>The path the hub is served at, from its <c>[WireHub]</c> attribute.</summary>
    public string Path { get; }

    /// <summary>True when the hub needs a signed-in user.</summary>
    public bool RequiresSignIn { get; internal set; }

    /// <summary>The full name of the hub's client interface.</summary>
    public string ClientInterfaceClrName { get; internal set; }

    /// <summary>The methods the frontend can call, in declaration order.</summary>
    public IReadOnlyList<HubMethod> Methods => MethodList;

    /// <summary>The messages the hub sends, in declaration order.</summary>
    public IReadOnlyList<HubMessage> Messages => MessageList;

    internal List<HubMethod> MethodList { get; } = new();

    internal List<HubMessage> MessageList { get; } = new();
}
