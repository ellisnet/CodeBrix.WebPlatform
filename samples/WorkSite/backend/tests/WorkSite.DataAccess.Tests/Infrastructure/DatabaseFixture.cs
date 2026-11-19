using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.DataAccess.Schema;
using Xunit;

namespace WorkSite.DataAccess.Tests.Infrastructure;

/// <summary>Class fixture: a fresh, fully migrated database for one test class.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    /// <summary>The connection string of this class's database.</summary>
    public string ConnectionString { get; private set; }

    /// <summary>A data source for this class's database.</summary>
    public PgSqlDataSource DataSource { get; private set; }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        ConnectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("dal"));
        await new DatabaseMigrator(ConnectionString).MigrateAsync(TestContext.Current.CancellationToken);
        DataSource = PgSqlDataSource.Create(ConnectionString);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (DataSource != null)
        {
            await DataSource.DisposeAsync();
        }
    }
}
