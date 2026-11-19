using System;

namespace WorkSite.DataAccess.Errors;

/// <summary>
/// A value that must be unique is already in use: a username or email address, a client name for
/// the user, or a project name for the client.
/// </summary>
public sealed class DuplicateValueException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="field">The camelCase name of the field holding the duplicate value.</param>
    /// <param name="innerException">The database error, if any.</param>
    public DuplicateValueException(string field, Exception innerException = null)
        : base($"The {field} is already in use.", innerException)
    {
        Field = field;
    }

    /// <summary>The camelCase name of the field holding the duplicate value.</summary>
    public string Field { get; }
}
