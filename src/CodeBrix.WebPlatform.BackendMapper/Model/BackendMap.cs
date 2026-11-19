using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>
/// Everything a frontend needs to know to talk to a backend: its REST controllers, its SignalR
/// hubs, and the contract classes and enums they use. This is what the templates are applied to.
/// </summary>
public sealed class BackendMap
{
    internal BackendMap(string assemblyName)
    {
        AssemblyName = assemblyName;
    }

    /// <summary>The name of the backend assembly that was read.</summary>
    public string AssemblyName { get; }

    /// <summary>
    /// The value the backend demands in <c>X-Requested-With</c> on every state-changing request,
    /// from its <c>[assembly: WireRequestMarker]</c>; null when it declares none.
    /// </summary>
    public string RequestMarker { get; internal set; }

    /// <summary>
    /// The action that renews an expired session (<c>[WireSessionRefresh]</c>); null when the
    /// backend has none (then nothing of it needs sign-in).
    /// </summary>
    public RestEndpoint SessionRefresh { get; internal set; }

    /// <summary>The controller <see cref="SessionRefresh"/> belongs to; null when there is none.</summary>
    public RestController SessionRefreshController { get; internal set; }

    /// <summary>True when any endpoint or hub of the backend needs a signed-in user.</summary>
    public bool RequiresSignIn => ControllerList.Exists(c => c.Endpoints.Any(e => e.RequiresSignIn)) || HubList.Exists(h => h.RequiresSignIn);

    /// <summary>The REST controllers, ordered by name.</summary>
    public IReadOnlyList<RestController> Controllers => ControllerList;

    /// <summary>The SignalR hubs, ordered by name.</summary>
    public IReadOnlyList<HubMap> Hubs => HubList;

    /// <summary>The contract classes, ordered by name.</summary>
    public IReadOnlyList<WireStruct> Structs => StructList;

    /// <summary>The enums, ordered by name.</summary>
    public IReadOnlyList<WireEnum> Enums => EnumList;

    internal List<RestController> ControllerList { get; } = new();

    internal List<HubMap> HubList { get; } = new();

    internal List<WireStruct> StructList { get; } = new();

    internal List<WireEnum> EnumList { get; } = new();
}
