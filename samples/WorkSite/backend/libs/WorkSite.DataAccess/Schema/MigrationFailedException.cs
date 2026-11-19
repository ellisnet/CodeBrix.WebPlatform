using System;

namespace WorkSite.DataAccess.Schema;

/// <summary>
/// A schema script failed. Its transaction was rolled back, so the recorded schema version is the
/// one before that script.
/// </summary>
public sealed class MigrationFailedException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="version">The version of the script that failed.</param>
    /// <param name="name">The name of the script that failed.</param>
    /// <param name="innerException">The database error.</param>
    public MigrationFailedException(int version, string name, Exception innerException)
        : base($"Schema script {version:0000}_{name} failed and was rolled back: {innerException.Message}", innerException)
    {
        Version = version;
        ScriptName = name;
    }

    /// <summary>The version of the script that failed.</summary>
    public int Version { get; }

    /// <summary>The name of the script that failed.</summary>
    public string ScriptName { get; }
}
