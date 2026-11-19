using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>GET /api/meta</c>.</summary>
[WireRequired]
public sealed class MetaResponse
{
    /// <summary>The application name.</summary>
    public string Name { get; set; }

    /// <summary>The application version.</summary>
    public string Version { get; set; }

    /// <summary>The demo account.</summary>
    public DemoAccountResponse DemoAccount { get; set; }
}
