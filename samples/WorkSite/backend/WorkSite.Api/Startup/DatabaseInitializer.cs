using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkSite.Api.Configuration;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Schema;
using WorkSite.DataAccess.Seeding;

namespace WorkSite.Api.Startup;

/// <summary>Runs at startup, before requests are served: migrates the database, then seeds the demo account.</summary>
public static class DatabaseInitializer
{
    /// <summary>Migrates the database and, when enabled, seeds the demo account.</summary>
    /// <param name="services">The application services.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var settings = services.GetRequiredService<WorkSiteSettings>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("WorkSite.Startup");

        var migrator = new DatabaseMigrator(settings.ConnectionString,
            services.GetRequiredService<ILoggerFactory>().CreateLogger<DatabaseMigrator>());
        var migration = await migrator.MigrateAsync(cancellationToken);
        if (migration.DatabaseCreated)
        {
            logger.LogInformation("Created database {Database}.", migrator.DatabaseName);
        }

        logger.LogInformation(
            migration.AppliedScripts.Count == 0
                ? "Database schema already current at version {Version} ({Applied} scripts applied)."
                : "Database schema now at version {Version} ({Applied} scripts applied).",
            migration.CurrentVersion, migration.AppliedScripts.Count);

        var demo = services.GetRequiredService<DemoAccountState>();
        if (!settings.Demo.Enabled)
        {
            logger.LogInformation("The demo account is disabled.");
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var seeder = new DemoDataSeeder(services.GetRequiredService<PgSqlDataSource>());
        var seed = await seeder.SeedAsync(new DemoSeedRequest
        {
            Username = settings.Demo.Username,
            PasswordHash = hasher.HashPassword(null, settings.Demo.Password),
            Today = DateOnly.FromDateTime(DateTime.UtcNow),
        }, cancellationToken);

        if (seed.Created)
        {
            logger.LogInformation(
                "Seeded the demo account {Username}: {Clients} clients, {Projects} projects, {Entries} time entries.",
                settings.Demo.Username, seed.ClientCount, seed.ProjectCount, seed.EntryCount);
            demo.Enabled = true;
        }
        else
        {
            var existing = await services.GetRequiredService<IUserRepository>()
                .FindByUsernameAsync(settings.Demo.Username, cancellationToken);
            demo.Enabled = existing is { IsDemo: true };
            if (demo.Enabled)
            {
                logger.LogInformation("The demo account {Username} already exists; it was left unchanged.",
                    settings.Demo.Username);
            }
            else
            {
                logger.LogWarning(
                    "The username {Username} belongs to an ordinary account, so the demo account is not offered.",
                    settings.Demo.Username);
            }
        }

        demo.Username = settings.Demo.Username;
    }
}
