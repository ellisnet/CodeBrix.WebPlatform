using System;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using Xunit;

namespace WorkSite.DataAccess.Tests.Infrastructure;

/// <summary>Creates rows for tests through the repositories, with unique names.</summary>
public static class TestData
{
    /// <summary>A short unique suffix.</summary>
    /// <returns>Eight hex characters.</returns>
    public static string Unique() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Creates a user with a unique username and email address.</summary>
    /// <param name="dataSource">The database.</param>
    /// <param name="prefix">The start of the username.</param>
    /// <returns>The user.</returns>
    public static Task<User> UserAsync(PgSqlDataSource dataSource, string prefix = "user")
    {
        var name = prefix + "_" + Unique();
        return new UserRepository(dataSource).CreateAsync(new NewUser
        {
            FirstName = "Test",
            LastName = "User",
            Email = name + "@example.com",
            Username = name,
            PasswordHash = "not-a-real-hash",
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Creates a client.</summary>
    /// <param name="dataSource">The database.</param>
    /// <param name="userId">The owner.</param>
    /// <param name="name">The name.</param>
    /// <param name="color">The colour.</param>
    /// <param name="archived">Whether it is archived.</param>
    /// <returns>The client.</returns>
    public static Task<Client> ClientAsync(PgSqlDataSource dataSource, Guid userId, string name = null,
        string color = "sky", bool archived = false)
        => new ClientRepository(dataSource).CreateAsync(userId, new ClientInput
        {
            Name = name ?? "Client " + Unique(),
            Color = color,
            IsArchived = archived,
        }, TestContext.Current.CancellationToken);

    /// <summary>Creates a project.</summary>
    /// <param name="dataSource">The database.</param>
    /// <param name="userId">The owner.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="name">The name.</param>
    /// <param name="rate">The hourly rate.</param>
    /// <param name="billable">The default billable flag.</param>
    /// <param name="archived">Whether it is archived.</param>
    /// <returns>The project.</returns>
    public static Task<Project> ProjectAsync(PgSqlDataSource dataSource, Guid userId, Guid clientId, string name = null,
        decimal? rate = 100m, bool billable = true, bool archived = false)
        => new ProjectRepository(dataSource).CreateAsync(userId, new ProjectInput
        {
            ClientId = clientId,
            Name = name ?? "Project " + Unique(),
            HourlyRate = rate,
            IsBillable = billable,
            IsArchived = archived,
        }, TestContext.Current.CancellationToken);

    /// <summary>Creates a time entry.</summary>
    /// <param name="dataSource">The database.</param>
    /// <param name="userId">The owner.</param>
    /// <param name="projectId">The project.</param>
    /// <param name="workDate">The work date.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="billable">Whether it is billable.</param>
    /// <param name="description">The description.</param>
    /// <returns>The entry.</returns>
    public static Task<TimeEntry> EntryAsync(PgSqlDataSource dataSource, Guid userId, Guid projectId, DateOnly workDate,
        decimal hours, bool billable = true, string description = "Work")
        => new TimeEntryRepository(dataSource).CreateAsync(userId, new TimeEntryInput
        {
            ProjectId = projectId,
            WorkDate = workDate,
            Hours = hours,
            IsBillable = billable,
            Description = description,
        }, TestContext.Current.CancellationToken);
}
