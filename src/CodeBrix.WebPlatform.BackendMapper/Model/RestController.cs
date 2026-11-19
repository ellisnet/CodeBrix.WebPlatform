using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One REST controller and its endpoints.</summary>
public sealed class RestController
{
    internal RestController(string name, string clrName)
    {
        Name = name;
        ClrName = clrName;
    }

    /// <summary>The controller's name without the <c>Controller</c> suffix.</summary>
    public string Name { get; }

    /// <summary>The full name of the .NET class.</summary>
    public string ClrName { get; }

    /// <summary>The endpoints, in declaration order.</summary>
    public IReadOnlyList<RestEndpoint> Endpoints => EndpointList;

    internal List<RestEndpoint> EndpointList { get; } = new();
}
