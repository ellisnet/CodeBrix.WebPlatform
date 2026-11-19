using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Scenarios;

/// <summary>User A's rows are invisible to, and untouchable by, user B through every repository.</summary>
public class UserIsolationScenario : IClassFixture<DatabaseFixture>
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    private readonly DatabaseFixture _database;

    public UserIsolationScenario(DatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task reads_never_return_another_users_rows()
    {
        //Arrange
        var (a, b) = await TwoUsersWithDataAsync();
        var token = TestContext.Current.CancellationToken;
        var ds = _database.DataSource;

        //Act
        var clientsSeenByB = await new ClientRepository(ds).ListAsync(b.UserId, true, token);
        var projectsSeenByB = await new ProjectRepository(ds).ListAsync(b.UserId, null, true, token);
        var entriesSeenByB = await new TimeEntryRepository(ds).QueryAsync(b.UserId, new TimeEntryFilter { PageSize = 100 }, token);
        var aClientAsB = await new ClientRepository(ds).GetAsync(b.UserId, a.ClientId, token);
        var aProjectAsB = await new ProjectRepository(ds).GetAsync(b.UserId, a.ProjectId, token);
        var aEntryAsB = await new TimeEntryRepository(ds).GetAsync(b.UserId, a.EntryId, token);
        var aProjectsFilteredByB = await new ProjectRepository(ds).ListAsync(b.UserId, a.ClientId, true, token);
        var entriesOfAClientAsB = await new TimeEntryRepository(ds).QueryAsync(b.UserId,
            new TimeEntryFilter { ClientId = a.ClientId }, token);
        var dashboardOfB = await new DashboardRepository(ds).GetAsync(b.UserId, Day, token);
        var reportOfB = await new ReportRepository(ds).GetSummaryAsync(b.UserId, new ReportQuery
        {
            From = Day.AddDays(-30),
            To = Day,
            GroupBy = ReportGroupBy.Client,
        }, token);
        var exportOfB = await new ReportRepository(ds).GetExportRowsAsync(b.UserId, Day.AddDays(-30), Day, a.ClientId, null, token);

        //Assert
        clientsSeenByB.Select(c => c.Id).Should().Equal(b.ClientId);
        projectsSeenByB.Select(p => p.Id).Should().Equal(b.ProjectId);
        entriesSeenByB.Items.Select(e => e.Id).Should().Equal(b.EntryId);
        entriesSeenByB.TotalHours.Should().Be(2m);
        aClientAsB.Should().BeNull();
        aProjectAsB.Should().BeNull();
        aEntryAsB.Should().BeNull();
        aProjectsFilteredByB.Should().BeEmpty();
        entriesOfAClientAsB.Items.Should().BeEmpty();
        dashboardOfB.HoursThisMonth.Should().Be(2m);
        dashboardOfB.RecentEntries.Select(e => e.Id).Should().Equal(b.EntryId);
        reportOfB.TotalHours.Should().Be(2m);
        reportOfB.Rows.Select(r => r.Key).Should().Equal(b.ClientId.ToString());
        exportOfB.Should().BeEmpty();
    }

    [Fact]
    public async Task writes_never_touch_another_users_rows()
    {
        //Arrange
        var (a, b) = await TwoUsersWithDataAsync();
        var token = TestContext.Current.CancellationToken;
        var ds = _database.DataSource;

        //Act
        var clientUpdate = await new ClientRepository(ds).UpdateAsync(b.UserId, a.ClientId,
            new ClientInput { Name = "Hijacked", Color = "red" }, token);
        var projectUpdate = await new ProjectRepository(ds).UpdateAsync(b.UserId, a.ProjectId,
            new ProjectInput { ClientId = b.ClientId, Name = "Hijacked" }, token);
        var entryUpdate = await new TimeEntryRepository(ds).UpdateAsync(b.UserId, a.EntryId,
            new TimeEntryInput { ProjectId = b.ProjectId, WorkDate = Day, Hours = 1m }, token);
        var entryDelete = await new TimeEntryRepository(ds).DeleteAsync(b.UserId, a.EntryId, token);
        var projectDelete = await new ProjectRepository(ds).DeleteAsync(b.UserId, a.ProjectId, token);
        var clientDelete = await new ClientRepository(ds).DeleteAsync(b.UserId, a.ClientId, token);

        //Assert
        clientUpdate.Should().BeNull();
        projectUpdate.Should().BeNull();
        entryUpdate.Should().BeNull();
        entryDelete.Should().BeFalse();
        projectDelete.Should().Be(DeleteOutcome.NotFound);
        clientDelete.Should().Be(DeleteOutcome.NotFound);
        (await new ClientRepository(ds).GetAsync(a.UserId, a.ClientId, token)).Name.Should().Be("A's client");
        (await new TimeEntryRepository(ds).GetAsync(a.UserId, a.EntryId, token)).Hours.Should().Be(5m);
    }

    private async Task<((Guid UserId, Guid ClientId, Guid ProjectId, Guid EntryId) A,
        (Guid UserId, Guid ClientId, Guid ProjectId, Guid EntryId) B)> TwoUsersWithDataAsync()
    {
        var ds = _database.DataSource;
        var userA = await TestData.UserAsync(ds, "a");
        var clientA = await TestData.ClientAsync(ds, userA.Id, "A's client");
        var projectA = await TestData.ProjectAsync(ds, userA.Id, clientA.Id);
        var entryA = await TestData.EntryAsync(ds, userA.Id, projectA.Id, Day, 5m);
        var userB = await TestData.UserAsync(ds, "b");
        var clientB = await TestData.ClientAsync(ds, userB.Id, "B's client");
        var projectB = await TestData.ProjectAsync(ds, userB.Id, clientB.Id);
        var entryB = await TestData.EntryAsync(ds, userB.Id, projectB.Id, Day, 2m);
        return ((userA.Id, clientA.Id, projectA.Id, entryA.Id), (userB.Id, clientB.Id, projectB.Id, entryB.Id));
    }
}
