using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;

namespace WorkSite.DataAccess.Schema;

/// <summary>Reads the applied schema version of the application database.</summary>
public sealed class SchemaVersionReader
{
    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the reader.</summary>
    /// <param name="dataSource">The application database.</param>
    public SchemaVersionReader(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>The highest applied schema version; 0 when none has been applied.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The version.</returns>
    public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN to_regclass('schema_version') IS NULL THEN 0 " +
            "ELSE (SELECT coalesce(max(version), 0) FROM schema_version) END",
            cancellationToken: cancellationToken);
    }
}
