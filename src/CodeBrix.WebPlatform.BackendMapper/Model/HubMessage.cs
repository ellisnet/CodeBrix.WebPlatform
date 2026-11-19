using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One message a hub sends to the frontend: a method of the hub's client interface.</summary>
public sealed class HubMessage
{
    internal HubMessage(string name)
    {
        Name = name;
    }

    /// <summary>The message's name, which is also its name on the wire.</summary>
    public string Name { get; }

    /// <summary>The arguments, in order.</summary>
    public IReadOnlyList<HubArgument> Arguments => ArgumentList;

    internal List<HubArgument> ArgumentList { get; } = new();
}
