using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>A (made-up) reminder shown to the signed-in user.</summary>
[WireRequired]
public sealed class Notification
{
    /// <summary>The notification's id.</summary>
    public Guid Id { get; set; }

    /// <summary>How it is shown.</summary>
    public NotificationKind Kind { get; set; }

    /// <summary>The sentence to show.</summary>
    public string Message { get; set; }

    /// <summary>When it was sent, UTC.</summary>
    public DateTime SentAt { get; set; }
}
