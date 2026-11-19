using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class DashboardRepositoryTests : IClassFixture<DatabaseFixture>
{
    //A Wednesday. This week is Monday 16 to Sunday 22 November; last week is 9 to 15 November.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private readonly DashboardRepository _dashboard;
    private readonly DatabaseFixture _database;

    public DashboardRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _dashboard = new DashboardRepository(database.DataSource);
    }

    [Fact]
    public async Task GetAsync_counts_weeks_from_monday()
    {
        //Arrange
        var (userId, projectId) = await UserWithProjectAsync();
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 8), 1m);   //Sunday before last week
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 9), 2m);   //last week's Monday
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 15), 3m);  //last week's Sunday
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 16), 4m);  //this Monday
        await TestData.EntryAsync(_database.DataSource, userId, projectId, Today, 5m);
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 19), 0.5m); //tomorrow

        //Act
        var dashboard = await _dashboard.GetAsync(userId, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.HoursToday.Should().Be(5m);
        dashboard.HoursThisWeek.Should().Be(9.5m);
        dashboard.HoursLastWeek.Should().Be(5m);
        dashboard.HoursThisMonth.Should().Be(15.5m);
    }

    [Fact]
    public async Task GetAsync_lists_the_last_fourteen_days_oldest_first_with_zeros()
    {
        //Arrange
        var (userId, projectId) = await UserWithProjectAsync();
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 4), 9m);  //one day too old
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 5), 2m);
        await TestData.EntryAsync(_database.DataSource, userId, projectId, new DateOnly(2026, 11, 5), 1m, billable: false);
        await TestData.EntryAsync(_database.DataSource, userId, projectId, Today, 4m);

        //Act
        var dashboard = await _dashboard.GetAsync(userId, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.DailyHours.Should().HaveCount(14);
        dashboard.DailyHours.Select(d => d.Date).Should().BeInAscendingOrder();
        dashboard.DailyHours[0].Date.Should().Be(new DateOnly(2026, 11, 5));
        dashboard.DailyHours[0].Hours.Should().Be(3m);
        dashboard.DailyHours[0].BillableHours.Should().Be(2m);
        dashboard.DailyHours[13].Date.Should().Be(Today);
        dashboard.DailyHours[13].Hours.Should().Be(4m);
        dashboard.DailyHours.Skip(1).Take(12).Should().OnlyContain(d => d.Hours == 0m && d.BillableHours == 0m);
    }

    [Fact]
    public async Task GetAsync_totals_billable_hours_and_amounts_for_the_month()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var rated = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, rate: 120m);
        var unrated = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, rate: null);
        await TestData.EntryAsync(_database.DataSource, user.Id, rated.Id, new DateOnly(2026, 11, 2), 2.5m);
        await TestData.EntryAsync(_database.DataSource, user.Id, rated.Id, new DateOnly(2026, 11, 3), 1m, billable: false);
        await TestData.EntryAsync(_database.DataSource, user.Id, unrated.Id, new DateOnly(2026, 11, 4), 3m);
        await TestData.EntryAsync(_database.DataSource, user.Id, rated.Id, new DateOnly(2026, 10, 31), 5m);

        //Act
        var dashboard = await _dashboard.GetAsync(user.Id, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.HoursThisMonth.Should().Be(6.5m);
        dashboard.BillableHoursThisMonth.Should().Be(5.5m);
        dashboard.AmountThisMonth.Should().Be(300m);
    }

    [Fact]
    public async Task GetAsync_lists_this_months_top_five_projects_with_client_colours()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id, "Teal Co", color: "teal");
        for (var i = 1; i <= 6; i++)
        {
            var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, "P" + i);
            await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, 2), i);
        }

        //Act
        var dashboard = await _dashboard.GetAsync(user.Id, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.TopProjects.Select(p => p.ProjectName).Should().Equal("P6", "P5", "P4", "P3", "P2");
        dashboard.TopProjects.Should().OnlyContain(p => p.Color == "teal" && p.ClientName == "Teal Co");
        dashboard.TopProjects[0].Hours.Should().Be(6m);
    }

    [Fact]
    public async Task GetAsync_counts_active_work_and_lists_the_eight_most_recent_entries()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var active = await TestData.ClientAsync(_database.DataSource, user.Id);
        var archived = await TestData.ClientAsync(_database.DataSource, user.Id, archived: true);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, active.Id);
        await TestData.ProjectAsync(_database.DataSource, user.Id, active.Id, archived: true);
        await TestData.ProjectAsync(_database.DataSource, user.Id, archived.Id);
        for (var day = 1; day <= 10; day++)
        {
            await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, day), 1m);
        }

        //Act
        var dashboard = await _dashboard.GetAsync(user.Id, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.ActiveClientCount.Should().Be(1);
        dashboard.ActiveProjectCount.Should().Be(1);
        dashboard.RecentEntries.Select(e => e.WorkDate.Day).Should().Equal(10, 9, 8, 7, 6, 5, 4, 3);
    }

    [Fact]
    public async Task GetAsync_for_a_new_user_is_all_zeros()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);

        //Act
        var dashboard = await _dashboard.GetAsync(user.Id, Today, TestContext.Current.CancellationToken);

        //Assert
        dashboard.HoursThisMonth.Should().Be(0m);
        dashboard.AmountThisMonth.Should().Be(0m);
        dashboard.DailyHours.Should().HaveCount(14).And.OnlyContain(d => d.Hours == 0m);
        dashboard.TopProjects.Should().BeEmpty();
        dashboard.RecentEntries.Should().BeEmpty();
    }

    private async Task<(Guid UserId, Guid ProjectId)> UserWithProjectAsync()
    {
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        return (user.Id, project.Id);
    }
}
