using System.Text.Json.Serialization;

namespace WorkSite.Core.Hubs;

/// <summary>What happened to the thing a data-changed notice is about.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeKind
{
    /// <summary>It was created.</summary>
    Created,

    /// <summary>It was changed.</summary>
    Updated,

    /// <summary>It was deleted.</summary>
    Deleted,
}
