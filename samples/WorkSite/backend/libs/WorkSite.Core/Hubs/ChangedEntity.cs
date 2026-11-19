using System.Text.Json.Serialization;

namespace WorkSite.Core.Hubs;

/// <summary>The kind of thing a data-changed notice is about.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangedEntity
{
    /// <summary>A client.</summary>
    Client,

    /// <summary>A project.</summary>
    Project,

    /// <summary>A time entry.</summary>
    TimeEntry,
}
