using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using SilverAssertions;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Schema;
using WorkSite.DataAccess.Seeding;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Seeding;

public class DemoDataSeederTests : IClassFixture<DatabaseFixture>
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    private readonly DatabaseFixture _database;

    public DemoDataSeederTests(DatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task SeedAsync_creates_a_demo_account_with_a_plausible_history()
    {
        //Arrange
        var seeder = new DemoDataSeeder(_database.DataSource);
        var username = "demo_" + TestData.Unique();

        //Act
        var result = await seeder.SeedAsync(Request(username), TestContext.Current.CancellationToken);

        //Assert
        result.Created.Should().BeTrue();
        result.ClientCount.Should().BeInRange(4, 8);
        result.ProjectCount.Should().BeGreaterThanOrEqualTo(result.ClientCount * 2 - 2);
        result.EntryCount.Should().BeInRange(200, 500);
        result.TotalHours.Should().BeInRange(450m, 900m);

        var user = await new UserRepository(_database.DataSource).FindByUsernameAsync(username, TestContext.Current.CancellationToken);
        user.IsDemo.Should().BeTrue();
        user.PasswordHash.Should().Be("demo-hash");

        var entries = await AllEntriesAsync(user.Id);
        entries.Should().HaveCount(result.EntryCount);
        entries.Should().OnlyContain(e => e.WorkDate <= Today && e.WorkDate > Today.AddDays(-DemoDataSeeder.HistoryDays));
        entries.Should().OnlyContain(e => e.Hours > 0m && e.Hours <= 24m && decimal.Round(e.Hours, 2) == e.Hours);
        var weekdayShare = entries.Count(e => e.WorkDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                           / (double)entries.Count;
        weekdayShare.Should().BeGreaterThan(0.9);
        entries.GroupBy(e => e.WorkDate).Should().OnlyContain(day => day.Sum(e => e.Hours) <= 12m);
        entries.Select(e => e.Description).Distinct().Count().Should().BeGreaterThan(20);
        var billableShare = entries.Count(e => e.IsBillable) / (double)entries.Count;
        billableShare.Should().BeInRange(0.6, 0.95);
        entries.Select(e => e.WorkDate).Max().Should().BeOnOrAfter(Today.AddDays(-3));
        entries.Select(e => e.WorkDate).Min().Should().BeOnOrBefore(Today.AddDays(-DemoDataSeeder.HistoryDays + 7));
    }

    [Fact]
    public async Task SeedAsync_does_nothing_when_the_demo_user_exists()
    {
        //Arrange
        var seeder = new DemoDataSeeder(_database.DataSource);
        var username = "demo_" + TestData.Unique();
        var first = await seeder.SeedAsync(Request(username), TestContext.Current.CancellationToken);

        //Act
        var second = await seeder.SeedAsync(Request(username.ToUpperInvariant()), TestContext.Current.CancellationToken);

        //Assert
        second.Created.Should().BeFalse();
        second.UserId.Should().Be(first.UserId);
        (await AllEntriesAsync(first.UserId)).Should().HaveCount(first.EntryCount);
    }

    [Fact]
    public async Task SeedAsync_never_overwrites_an_ordinary_account_with_that_username()
    {
        //Arrange
        var existing = await TestData.UserAsync(_database.DataSource, "taken");

        //Act
        var result = await new DemoDataSeeder(_database.DataSource).SeedAsync(Request(existing.Username),
            TestContext.Current.CancellationToken);

        //Assert
        result.Created.Should().BeFalse();
        result.UserId.Should().Be(existing.Id);
        var user = await new UserRepository(_database.DataSource).GetByIdAsync(existing.Id, TestContext.Current.CancellationToken);
        user.IsDemo.Should().BeFalse();
        user.PasswordHash.Should().Be(existing.PasswordHash);
        (await AllEntriesAsync(existing.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_generates_the_same_history_for_the_same_date()
    {
        //Arrange
        var otherDatabase = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("seed"));
        await new DatabaseMigrator(otherDatabase).MigrateAsync(TestContext.Current.CancellationToken);
        await using var otherSource = PgSqlDataSource.Create(otherDatabase);
        var username = "demo_" + TestData.Unique();

        //Act
        var first = await new DemoDataSeeder(_database.DataSource).SeedAsync(Request(username), TestContext.Current.CancellationToken);
        var second = await new DemoDataSeeder(otherSource).SeedAsync(Request(username), TestContext.Current.CancellationToken);

        //Assert
        second.EntryCount.Should().Be(first.EntryCount);
        second.TotalHours.Should().Be(first.TotalHours);
        var firstDays = (await AllEntriesAsync(first.UserId)).Select(e => (e.WorkDate, e.Hours, e.Description)).OrderBy(x => x).ToList();
        var secondDays = (await AllEntriesAsync(second.UserId, otherSource)).Select(e => (e.WorkDate, e.Hours, e.Description)).OrderBy(x => x).ToList();
        secondDays.Should().Equal(firstDays);
    }

    [Fact]
    public async Task SeedAsync_leaves_archived_projects_with_older_entries_only()
    {
        //Arrange
        var username = "demo_" + TestData.Unique();
        var result = await new DemoDataSeeder(_database.DataSource).SeedAsync(Request(username), TestContext.Current.CancellationToken);

        //Act
        var projects = await new ProjectRepository(_database.DataSource).ListAsync(result.UserId, null, true,
            TestContext.Current.CancellationToken);
        var entries = await AllEntriesAsync(result.UserId);

        //Assert
        var archivedIds = projects.Where(p => p.IsArchived).Select(p => p.Id).ToHashSet();
        archivedIds.Should().NotBeEmpty();
        entries.Where(e => archivedIds.Contains(e.ProjectId))
            .Should().OnlyContain(e => e.WorkDate < Today.AddDays(-DemoDataSeeder.HistoryDays / 2 + 1));
    }

    private static DemoSeedRequest Request(string username) => new()
    {
        Username = username,
        Email = username + "@worksite.example",
        PasswordHash = "demo-hash",
        Today = Today,
    };

    private async Task<IReadOnlyList<TimeEntry>> AllEntriesAsync(Guid userId, PgSqlDataSource source = null)
    {
        var repository = new TimeEntryRepository(source ?? _database.DataSource);
        var all = new List<TimeEntry>();
        for (var page = 1; ; page++)
        {
            var result = await repository.QueryAsync(userId, new TimeEntryFilter { Page = page, PageSize = 100 },
                TestContext.Current.CancellationToken);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0)
            {
                return all;
            }
        }
    }
}
