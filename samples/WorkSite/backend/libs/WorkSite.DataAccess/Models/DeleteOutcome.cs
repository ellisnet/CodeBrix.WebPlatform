namespace WorkSite.DataAccess.Models;

/// <summary>The result of deleting a client or project.</summary>
public enum DeleteOutcome
{
    /// <summary>The row was deleted.</summary>
    Deleted,

    /// <summary>No such row belongs to the user.</summary>
    NotFound,

    /// <summary>Other rows still depend on it (projects of a client, entries of a project).</summary>
    InUse,
}
