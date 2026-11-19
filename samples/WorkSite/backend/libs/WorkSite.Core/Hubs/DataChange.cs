using System;

namespace WorkSite.Core.Hubs;

/// <summary>A notice that one of the signed-in user's records changed.</summary>
public sealed class DataChange
{
    /// <summary>The kind of record.</summary>
    public ChangedEntity Entity { get; set; }

    /// <summary>What happened to it.</summary>
    public ChangeKind Kind { get; set; }

    /// <summary>The record's id.</summary>
    public Guid Id { get; set; }
}
