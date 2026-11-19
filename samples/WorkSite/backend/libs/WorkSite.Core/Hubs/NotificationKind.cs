namespace WorkSite.Core.Hubs;

/// <summary>How a notification is shown. Sent as its number.</summary>
public enum NotificationKind
{
    /// <summary>For information.</summary>
    Info = 0,

    /// <summary>Good news.</summary>
    Success = 1,

    /// <summary>Something to look at.</summary>
    Warning = 2,
}
