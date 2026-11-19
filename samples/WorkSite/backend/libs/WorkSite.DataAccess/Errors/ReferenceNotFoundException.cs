using System;

namespace WorkSite.DataAccess.Errors;

/// <summary>
/// A referenced row does not exist for this user: the client of a project, or the project of a
/// time entry.
/// </summary>
public sealed class ReferenceNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="field">The camelCase name of the field holding the reference.</param>
    /// <param name="innerException">The database error, if any.</param>
    public ReferenceNotFoundException(string field, Exception innerException = null)
        : base($"The {field} does not refer to one of the user's records.", innerException)
    {
        Field = field;
    }

    /// <summary>The camelCase name of the field holding the reference.</summary>
    public string Field { get; }
}
