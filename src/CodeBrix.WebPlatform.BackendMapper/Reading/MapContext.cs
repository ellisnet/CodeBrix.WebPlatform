using System.Collections.Generic;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>What the readers share while one backend is mapped: the type map and the problems found so far.</summary>
internal sealed class MapContext
{
    public MapContext(RustTypeMap types)
    {
        Types = types;
        TypeReader = new TypeReader(this);
    }

    public RustTypeMap Types { get; }

    public TypeReader TypeReader { get; }

    public List<MappingProblem> Problems { get; } = new();

    /// <summary>True once any action or hub that needs sign-in has been read, mapped or not.</summary>
    public bool SignInSeen { get; set; }

    /// <summary>Every action marked <c>[WireSessionRefresh]</c>, valid or not, as a location.</summary>
    public List<string> SessionRefreshLocations { get; } = new();

    public void Problem(string location, string message) => Problems.Add(new MappingProblem(location, message));
}
