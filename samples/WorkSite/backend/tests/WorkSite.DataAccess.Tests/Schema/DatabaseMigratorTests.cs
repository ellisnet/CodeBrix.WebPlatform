using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using SilverAssertions;
using WorkSite.DataAccess.Schema;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Schema;

public class DatabaseMigratorTests
{
    private static readonly MigrationScript AddNickname = new(2, "add_nickname",
        "ALTER TABLE users ADD COLUMN nickname text NOT NULL DEFAULT '';");

    private static readonly MigrationScript Broken = new(2, "broken",
        "CREATE TABLE half_done (id integer PRIMARY KEY);\nSELECT 1 / 0;");

    [Fact]
    public async Task MigrateAsync_creates_the_database_and_applies_every_script()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        var migrator = new DatabaseMigrator(connectionString);

        //Act
        var result = await migrator.MigrateAsync(TestContext.Current.CancellationToken);

        //Assert
        result.DatabaseCreated.Should().BeTrue();
        result.PreviousVersion.Should().Be(0);
        result.CurrentVersion.Should().Be(migrator.LatestVersion);
        result.AppliedScripts.Should().HaveCount(MigrationScript.LoadEmbedded().Count);
        (await migrator.GetCurrentVersionAsync(TestContext.Current.CancellationToken)).Should().Be(migrator.LatestVersion);
        (await TablesAsync(connectionString)).Should().Contain(["users", "sessions", "clients", "projects", "time_entries", "schema_version"]);
        var recorded = await ScalarAsync<string>(connectionString, "SELECT name FROM schema_version WHERE version = 1");
        recorded.Should().Be("initial");
    }

    [Fact]
    public async Task MigrateAsync_on_a_current_database_changes_nothing()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        await new DatabaseMigrator(connectionString).MigrateAsync(TestContext.Current.CancellationToken);
        var rowsBefore = await ScalarAsync<long>(connectionString, "SELECT count(*) FROM schema_version");

        //Act
        var result = await new DatabaseMigrator(connectionString).MigrateAsync(TestContext.Current.CancellationToken);

        //Assert
        result.DatabaseCreated.Should().BeFalse();
        result.AppliedScripts.Should().BeEmpty();
        result.PreviousVersion.Should().Be(result.CurrentVersion);
        (await ScalarAsync<long>(connectionString, "SELECT count(*) FROM schema_version")).Should().Be(rowsBefore);
    }

    [Fact]
    public async Task MigrateAsync_upgrades_an_older_version_and_keeps_its_data()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        var initial = MigrationScript.LoadEmbedded().Where(s => s.Version == 1).ToList();
        await new DatabaseMigrator(connectionString, initial).MigrateAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connectionString,
            "INSERT INTO users (id, first_name, last_name, email, username, password_hash) " +
            "VALUES (gen_random_uuid(), 'Ada', 'Lovelace', 'ada@example.com', 'ada', 'hash')");

        //Act
        var result = await new DatabaseMigrator(connectionString, [.. initial, AddNickname])
            .MigrateAsync(TestContext.Current.CancellationToken);

        //Assert
        result.PreviousVersion.Should().Be(1);
        result.CurrentVersion.Should().Be(2);
        result.AppliedScripts.Should().ContainSingle().Which.Name.Should().Be("add_nickname");
        (await ScalarAsync<string>(connectionString, "SELECT first_name FROM users WHERE username = 'ada'")).Should().Be("Ada");
        (await ScalarAsync<string>(connectionString, "SELECT nickname FROM users WHERE username = 'ada'")).Should().Be("");
    }

    [Fact]
    public async Task MigrateAsync_rolls_back_a_failing_script_and_keeps_the_recorded_version()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        var initial = MigrationScript.LoadEmbedded().Where(s => s.Version == 1).ToList();
        await new DatabaseMigrator(connectionString, initial).MigrateAsync(TestContext.Current.CancellationToken);
        var migrator = new DatabaseMigrator(connectionString, [.. initial, Broken]);

        //Act
        Func<Task> act = () => migrator.MigrateAsync(TestContext.Current.CancellationToken);

        //Assert
        var failure = await act.Should().ThrowAsync<MigrationFailedException>();
        failure.Which.Version.Should().Be(2);
        failure.Which.InnerException.Should().BeOfType<PostgresException>();
        (await migrator.GetCurrentVersionAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await TablesAsync(connectionString)).Should().NotContain("half_done");
    }

    [Fact]
    public async Task MigrateAsync_applies_each_script_once_when_instances_start_together()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        var migrators = Enumerable.Range(0, 4).Select(_ => new DatabaseMigrator(connectionString)).ToList();

        //Act
        var results = await Task.WhenAll(migrators.Select(m => m.MigrateAsync(TestContext.Current.CancellationToken)));

        //Assert
        results.Sum(r => r.AppliedScripts.Count).Should().Be(MigrationScript.LoadEmbedded().Count);
        results.Should().OnlyContain(r => r.CurrentVersion == migrators[0].LatestVersion);
        (await ScalarAsync<long>(connectionString, "SELECT count(*) FROM schema_version"))
            .Should().Be(MigrationScript.LoadEmbedded().Count);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_is_zero_before_any_script_has_run()
    {
        //Arrange
        var connectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("mig"));
        var migrator = new DatabaseMigrator(connectionString, []);
        await migrator.MigrateAsync(TestContext.Current.CancellationToken);

        //Act
        var version = await migrator.GetCurrentVersionAsync(TestContext.Current.CancellationToken);

        //Assert
        version.Should().Be(0);
        migrator.LatestVersion.Should().Be(0);
    }

    [Fact]
    public void Constructor_rejects_two_scripts_with_the_same_version()
    {
        //Arrange
        var scripts = new List<MigrationScript> { new(1, "a", "SELECT 1"), new(1, "b", "SELECT 2") };

        //Act
        Action act = () => _ = new DatabaseMigrator("Host=localhost;Database=x", scripts);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QuoteIdentifier_doubles_embedded_quotes()
        => DatabaseMigrator.QuoteIdentifier("odd\"name").Should().Be("\"odd\"\"name\"");

    private static async Task<List<string>> TablesAsync(string connectionString)
    {
        await using var connection = new PgSqlConnection(connectionString);
        var tables = await connection.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
            cancellationToken: TestContext.Current.CancellationToken);
        return tables.ToList();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new PgSqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<T>(sql, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new PgSqlConnection(connectionString);
        await connection.ExecuteAsync(sql, cancellationToken: TestContext.Current.CancellationToken);
    }
}
