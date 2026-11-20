using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One REST endpoint: an action of a controller.</summary>
public sealed class RestEndpoint
{
    internal RestEndpoint(string actionName, string httpMethod, string routeTemplate)
    {
        ActionName = actionName;
        HttpMethod = httpMethod;
        RouteTemplate = routeTemplate;
    }

    /// <summary>The .NET method name.</summary>
    public string ActionName { get; }

    /// <summary>The HTTP method in upper case, such as <c>GET</c>.</summary>
    public string HttpMethod { get; }

    /// <summary>The whole path template, starting with <c>/</c>, such as
    /// <c>/api/clients/{id}</c> (constraints removed).</summary>
    public string RouteTemplate { get; }

    /// <summary>The path split into fixed text and parameter places.</summary>
    public IReadOnlyList<RouteSegment> Route => RouteList;

    /// <summary>The values the endpoint takes: route values in path order, then the body, then
    /// the query values.</summary>
    public IReadOnlyList<EndpointParameter> Parameters => ParameterList;

    /// <summary>True when the endpoint needs a signed-in user.</summary>
    public bool RequiresSignIn { get; internal set; }

    /// <summary>True when the answer is a file (<c>[WireFile]</c>), not JSON: the generated code gets an
    /// address builder and a reader of the bytes for it.</summary>
    public bool IsFile { get; internal set; }

    /// <summary>True when this is the action that renews an expired session
    /// (<c>[WireSessionRefresh]</c>).</summary>
    public bool IsSessionRefresh { get; internal set; }

    /// <summary>The status code of the success answer.</summary>
    public int SuccessStatus { get; internal set; }

    /// <summary>The type of the success answer's body; null when it has none.</summary>
    public WireType Response { get; internal set; }

    internal List<RouteSegment> RouteList { get; } = new();

    internal List<EndpointParameter> ParameterList { get; } = new();
}
