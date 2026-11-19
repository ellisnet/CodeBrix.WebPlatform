using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class TimeEntryRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly TimeEntryRepository _entries;
    private readonly DatabaseFixture _database;

    public TimeEntryRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _entries = new TimeEntryRepository(database.DataSource);
    }

    [Fact]
    public async Task CreateAsync_stores_the_entry_with_project_and_client_names()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id, "Riverbend");
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, "Reminders");

        //Act
        var entry = await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, 16), 3.75m,
            billable: false, description: "Templates");

        //Assert
        entry.ProjectId.Should().Be(project.Id);
        entry.ProjectName.Should().Be("Reminders");
        entry.ClientId.Should().Be(client.Id);
        entry.ClientName.Should().Be("Riverbend");
        entry.WorkDate.Should().Be(new DateOnly(2026, 11, 16));
        entry.Hours.Should().Be(3.75m);
        entry.IsBillable.Should().BeFalse();
        entry.Description.Should().Be("Templates");
        entry.UpdatedAt.Should().Be(entry.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_rejects_another_users_project()
    {
        //Arrange
        var owner = await TestData.UserAsync(_database.DataSource);
        var intruder = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, owner.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, owner.Id, client.Id);

        //Act
        Func<Task> act = () => TestData.EntryAsync(_database.DataSource, intruder.Id, project.Id, new DateOnly(2026, 11, 2), 1m);

        //Assert
        (await act.Should().ThrowAsync<ReferenceNotFoundException>()).Which.Field.Should().Be("projectId");
    }

    [Fact]
    public async Task QueryAsync_filters_orders_newest_first_and_totals_the_whole_set()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var clientA = await TestData.ClientAsync(_database.DataSource, user.Id);
        var clientB = await TestData.ClientAsync(_database.DataSource, user.Id);
        var projectA = await TestData.ProjectAsync(_database.DataSource, user.Id, clientA.Id);
        var projectB = await TestData.ProjectAsync(_database.DataSource, user.Id, clientB.Id);
        for (var day = 1; day <= 10; day++)
        {
            await TestData.EntryAsync(_database.DataSource, user.Id, day % 2 == 0 ? projectA.Id : projectB.Id,
                new DateOnly(2026, 10, day), 1.5m, billable: day <= 6);
        }

        //Act
        var firstPage = await _entries.QueryAsync(user.Id, new TimeEntryFilter { Page = 1, PageSize = 4 },
            TestContext.Current.CancellationToken);
        var lastPage = await _entries.QueryAsync(user.Id, new TimeEntryFilter { Page = 3, PageSize = 4 },
            TestContext.Current.CancellationToken);
        var ranged = await _entries.QueryAsync(user.Id, new TimeEntryFilter
        {
            From = new DateOnly(2026, 10, 3),
            To = new DateOnly(2026, 10, 7),
        }, TestContext.Current.CancellationToken);
        var clientOnly = await _entries.QueryAsync(user.Id, new TimeEntryFilter { ClientId = clientA.Id },
            TestContext.Current.CancellationToken);
        var projectOnly = await _entries.QueryAsync(user.Id, new TimeEntryFilter { ProjectId = projectB.Id },
            TestContext.Current.CancellationToken);
        var nonBillable = await _entries.QueryAsync(user.Id, new TimeEntryFilter { Billable = false },
            TestContext.Current.CancellationToken);

        //Assert
        firstPage.TotalCount.Should().Be(10);
        firstPage.TotalHours.Should().Be(15m);
        firstPage.Page.Should().Be(1);
        firstPage.PageSize.Should().Be(4);
        firstPage.Items.Select(e => e.WorkDate.Day).Should().Equal(10, 9, 8, 7);
        lastPage.Items.Select(e => e.WorkDate.Day).Should().Equal(2, 1);
        lastPage.TotalHours.Should().Be(15m);
        ranged.Items.Select(e => e.WorkDate.Day).Should().Equal(7, 6, 5, 4, 3);
        ranged.TotalHours.Should().Be(7.5m);
        clientOnly.Items.Should().OnlyContain(e => e.ClientId == clientA.Id).And.HaveCount(5);
        projectOnly.Items.Should().OnlyContain(e => e.ProjectId == projectB.Id).And.HaveCount(5);
        nonBillable.Items.Select(e => e.WorkDate.Day).Should().Equal(10, 9, 8, 7);
    }

    [Fact]
    public async Task QueryAsync_orders_entries_of_one_day_by_creation_time()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        var first = await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 9, 1), 1m);
        var second = await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 9, 1), 2m);

        //Act
        var page = await _entries.QueryAsync(user.Id, new TimeEntryFilter(), TestContext.Current.CancellationToken);

        //Assert
        page.Items.Select(e => e.Id).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_values_and_moves_updated_at()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        var other = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, "Other");
        var entry = await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 9, 1), 1m);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        //Act
        var updated = await _entries.UpdateAsync(user.Id, entry.Id, new TimeEntryInput
        {
            ProjectId = other.Id,
            WorkDate = new DateOnly(2026, 9, 2),
            Hours = 8m,
            Description = "Changed",
            IsBillable = false,
        }, TestContext.Current.CancellationToken);

        //Assert
        updated.ProjectId.Should().Be(other.Id);
        updated.ProjectName.Should().Be("Other");
        updated.WorkDate.Should().Be(new DateOnly(2026, 9, 2));
        updated.Hours.Should().Be(8m);
        updated.Description.Should().Be("Changed");
        updated.IsBillable.Should().BeFalse();
        updated.CreatedAt.Should().Be(entry.CreatedAt);
        updated.UpdatedAt.Should().BeAfter(entry.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_entry_once()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        var entry = await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 9, 1), 1m);

        //Act
        var first = await _entries.DeleteAsync(user.Id, entry.Id, TestContext.Current.CancellationToken);
        var second = await _entries.DeleteAsync(user.Id, entry.Id, TestContext.Current.CancellationToken);

        //Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        (await _entries.GetAsync(user.Id, entry.Id, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task ListRecentAsync_returns_the_newest_entries_first()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        foreach (var day in new[] { 5, 1, 9, 3 })
        {
            await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 8, day), 1m);
        }

        //Act
        var recent = await _entries.ListRecentAsync(user.Id, 3, TestContext.Current.CancellationToken);

        //Assert
        recent.Select(e => e.WorkDate.Day).Should().Equal(9, 5, 3);
    }
}
