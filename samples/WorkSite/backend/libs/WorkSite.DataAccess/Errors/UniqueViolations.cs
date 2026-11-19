using System;
using System.Collections.Generic;
using CodeBrix.PostgresClient;

namespace WorkSite.DataAccess.Errors;

/// <summary>Turns PostgreSQL unique-index violations into <see cref="DuplicateValueException"/>.</summary>
internal static class UniqueViolations
{
    private static readonly Dictionary<string, string> FieldsByIndex = new(StringComparer.Ordinal)
    {
        ["ux_users_email"] = "email",
        ["ux_users_username"] = "username",
        ["ux_clients_user_name"] = "name",
        ["ux_projects_client_name"] = "name",
    };

    /// <summary>
    /// Returns a <see cref="DuplicateValueException"/> for a unique violation on one of the known
    /// indexes, or null for any other error.
    /// </summary>
    /// <param name="exception">The server error.</param>
    /// <returns>The translated exception, or null.</returns>
    public static DuplicateValueException Translate(PostgresException exception)
    {
        if (exception.SqlState != PostgresErrorCodes.UniqueViolation || exception.ConstraintName == null)
        {
            return null;
        }

        return FieldsByIndex.TryGetValue(exception.ConstraintName, out var field)
            ? new DuplicateValueException(field, exception)
            : null;
    }
}
