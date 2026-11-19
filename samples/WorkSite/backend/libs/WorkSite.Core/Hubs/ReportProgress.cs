using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>One step of a report being generated.</summary>
[WireRequired]
public sealed class ReportProgress
{
    /// <summary>How far along the report is, 0 to 100.</summary>
    public int Percent { get; set; }

    /// <summary>What is being done right now.</summary>
    public string Stage { get; set; }

    /// <summary>True for the last step.</summary>
    public bool IsComplete { get; set; }
}
