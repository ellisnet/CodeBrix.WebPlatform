using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>GET /api/health</c>.</summary>
[WireRequired]
public sealed class HealthResponse
{
    /// <summary>"ok" when the database answers.</summary>
    public string Status { get; set; }

    /// <summary>The applied schema version.</summary>
    public int DatabaseVersion { get; set; }
}
