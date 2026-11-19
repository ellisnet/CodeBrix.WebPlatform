using System.Collections.Generic;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;

namespace CodeBrix.WebPlatform.BackendMapper;

/// <summary>What <see cref="BackendMapper.Map(BackendMapOptions)"/> reads and how.</summary>
public sealed class BackendMapOptions
{
    /// <summary>
    /// The compiled backend assembly, such as <c>bin/Release/net10.0/MyApp.Api.dll</c>. The
    /// assemblies beside it (the application's other projects and its packages) are found by
    /// themselves.
    /// </summary>
    public string AssemblyPath { get; set; }

    /// <summary>
    /// Extra folders or assembly files to resolve references from. Normally empty: the .NET and
    /// ASP.NET Core assemblies of the installed runtime are found by themselves. Name them here
    /// when they are not, or to read against a particular reference pack.
    /// </summary>
    public IList<string> ReferencePaths { get; } = new List<string>();

    /// <summary>The .NET-to-Rust type mapping. Starts with the platform's defaults; change it to
    /// override them for the whole application.</summary>
    public RustTypeMap Types { get; set; } = new RustTypeMap();
}
