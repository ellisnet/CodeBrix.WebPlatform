namespace WorkSite.Core.Hubs;

/// <summary>A notice that the session behind a connection has ended.</summary>
public sealed class SessionEnded
{
    /// <summary>Why it ended.</summary>
    public SessionEndReason Reason { get; set; }
}
