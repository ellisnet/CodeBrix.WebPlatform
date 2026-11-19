using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkSite.DataAccess.Schema;

/// <summary>
/// Brings a WorkSite database up to the newest schema version.
/// </summary>
/// <remarks>
/// <see cref="MigrateAsync"/> (1) connects to the server's maintenance database and creates the
/// application database when it does not exist; (2) takes a PostgreSQL advisory lock, so several
/// application instances starting at once apply each script exactly once; (3) creates the
/// <c>schema_version</c> table when missing; (4) applies every script newer than the recorded
/// version, each in its own transaction, recording its version, name and time. Scripts only ever
/// add to the schema. A failing script is rolled back and reported with
/// <see cref="MigrationFailedException"/>, leaving the recorded version unchanged.
/// </remarks>
public sealed class DatabaseMigrator
{
    /// <summary>The advisory lock key every WorkSite migrator takes ("WorkSite" in ASCII).</summary>
    public const long AdvisoryLockKey = 0x576F726B53697465;

    /// <summary>The database the migrator connects to when it creates the application database.</summary>
    public const string DefaultMaintenanceDatabase = "postgres";

    private readonly string _connectionString;
    private readonly string _maintenanceDatabase;
    private readonly IReadOnlyList<MigrationScript> _scripts;
    private readonly ILogger _logger;

    /// <summary>Creates a migrator that applies the built-in scripts.</summary>
    /// <param name="connectionString">The connection string of the application database.</param>
    /// <param name="logger">Where progress is logged; null logs nothing.</param>
    public DatabaseMigrator(string connectionString, ILogger logger = null)
        : this(connectionString, MigrationScript.LoadEmbedded(), logger)
    {
    }

    /// <summary>Creates a migrator that applies the given scripts.</summary>
    /// <param name="connectionString">The connection string of the application database.</param>
    /// <param name="scripts">The scripts, with unique positive versions.</param>
    /// <param name="logger">Where progress is logged; null logs nothing.</param>
    /// <param name="maintenanceDatabase">The database used to create the application database.</param>
    public DatabaseMigrator(string connectionString, IReadOnlyList<MigrationScript> scripts, ILogger logger = null,
        string maintenanceDatabase = DefaultMaintenanceDatabase)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A connection string is required.", nameof(connectionString));
        }

        ArgumentNullException.ThrowIfNull(scripts);
        var duplicate = scripts.GroupBy(s => s.Version).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"More than one migration script has version {duplicate.Key}.", nameof(scripts));
        }

        _connectionString = connectionString;
        _maintenanceDatabase = maintenanceDatabase;
        _scripts = scripts.OrderBy(s => s.Version).ToList();
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The newest version among the scripts this migrator knows.</summary>
    public int LatestVersion => _scripts.Count == 0 ? 0 : _scripts[_scripts.Count - 1].Version;

    /// <summary>The name of the application database, from the connection string.</summary>
    public string DatabaseName => new PgSqlConnectionStringBuilder(_connectionString).Database;

    /// <summary>Creates the database if needed and applies every pending script.</summary>
    /// <param name="cancellationToken">Cancels the run between statements.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="MigrationFailedException">A script failed; it was rolled back.</exception>
    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var created = await EnsureDatabaseExistsAsync(cancellationToken);

        await using var connection = new PgSqlConnection(WithoutPooling(_connectionString));
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync("SELECT pg_advisory_lock(@key)", new { key = AdvisoryLockKey },
            cancellationToken: cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS schema_version (" +
                "version integer PRIMARY KEY, " +
                "name text NOT NULL, " +
                "applied_at timestamptz NOT NULL DEFAULT now())",
                cancellationToken: cancellationToken);

            var previous = await ReadVersionAsync(connection, cancellationToken);
            var applied = new List<MigrationScript>();
            foreach (var script in _scripts.Where(s => s.Version > previous))
            {
                await ApplyAsync(connection, script, cancellationToken);
                applied.Add(script);
            }

            var current = applied.Count == 0 ? previous : applied[applied.Count - 1].Version;
            if (applied.Count == 0)
            {
                _logger.LogInformation("Database {Database} is already current at schema version {Version}.",
                    DatabaseName, current);
            }
            else
            {
                _logger.LogInformation("Applied {Count} schema script(s) to {Database}: version {Previous} -> {Current}.",
                    applied.Count, DatabaseName, previous, current);
            }

            return new MigrationResult(created, previous, current, applied);
        }
        finally
        {
            await connection.ExecuteAsync("SELECT pg_advisory_unlock(@key)", new { key = AdvisoryLockKey },
                cancellationToken: CancellationToken.None);
        }
    }

    /// <summary>Reads the recorded schema version; 0 when no script has been applied.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The recorded version.</returns>
    public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new PgSqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await ReadVersionAsync(connection, cancellationToken);
    }

    private static async Task<int> ReadVersionAsync(PgSqlConnection connection, CancellationToken cancellationToken)
    {
        var tableExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('schema_version') IS NOT NULL", cancellationToken: cancellationToken);
        if (!tableExists)
        {
            return 0;
        }

        return await connection.ExecuteScalarAsync<int>(
            "SELECT coalesce(max(version), 0) FROM schema_version", cancellationToken: cancellationToken);
    }

    private async Task ApplyAsync(PgSqlConnection connection, MigrationScript script, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying schema script {Version:0000}_{Name} to {Database}.",
            script.Version, script.Name, DatabaseName);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = new PgSqlCommand(script.Sql, connection, transaction))
            {
                command.CommandTimeout = 0;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await connection.ExecuteAsync(
                "INSERT INTO schema_version (version, name, applied_at) VALUES (@version, @name, @appliedAt)",
                new { version = script.Version, name = script.Name, appliedAt = DateTime.UtcNow },
                transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(e, "Schema script {Version:0000}_{Name} failed and was rolled back.", script.Version, script.Name);
            throw new MigrationFailedException(script.Version, script.Name, e);
        }
    }

    private async Task<bool> EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new PgSqlConnectionStringBuilder(_connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("The WorkSite connection string must name a database (Database=...).");
        }

        builder.Database = _maintenanceDatabase;
        builder.Pooling = false;
        await using var connection = new PgSqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @database)",
            new { database }, cancellationToken: cancellationToken);
        if (exists)
        {
            return false;
        }

        try
        {
            await connection.ExecuteAsync("CREATE DATABASE " + QuoteIdentifier(database), cancellationToken: cancellationToken);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.DuplicateDatabase
                                          || (e.SqlState == PostgresErrorCodes.UniqueViolation
                                              && e.ConstraintName == "pg_database_datname_index"))
        {
            //Another instance created it between the check and the CREATE. PostgreSQL reports a
            //simultaneous CREATE as a unique violation on the catalog rather than as 42P04.
            return false;
        }

        _logger.LogInformation("Created database {Database}.", database);
        return true;
    }

    private static string WithoutPooling(string connectionString)
        => new PgSqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

    /// <summary>Quotes a PostgreSQL identifier, doubling any embedded double quotes.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    internal static string QuoteIdentifier(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
