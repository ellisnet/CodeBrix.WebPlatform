using System.Collections.Generic;

namespace WorkSite.DataAccess.Schema;

/// <summary>What one run of <see cref="DatabaseMigrator.MigrateAsync"/> did.</summary>
public sealed class MigrationResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="databaseCreated">Whether the application database had to be created.</param>
    /// <param name="previousVersion">The schema version before the run.</param>
    /// <param name="currentVersion">The schema version after the run.</param>
    /// <param name="appliedScripts">The scripts applied, oldest first.</param>
    public MigrationResult(bool databaseCreated, int previousVersion, int currentVersion, IReadOnlyList<MigrationScript> appliedScripts)
    {
        DatabaseCreated = databaseCreated;
        PreviousVersion = previousVersion;
        CurrentVersion = currentVersion;
        AppliedScripts = appliedScripts;
    }

    /// <summary>True when the application database did not exist and was created.</summary>
    public bool DatabaseCreated { get; }

    /// <summary>The schema version recorded before the run (0 for a new database).</summary>
    public int PreviousVersion { get; }

    /// <summary>The schema version recorded after the run.</summary>
    public int CurrentVersion { get; }

    /// <summary>The scripts this run applied, oldest first; empty when the database was already current.</summary>
    public IReadOnlyList<MigrationScript> AppliedScripts { get; }
}
