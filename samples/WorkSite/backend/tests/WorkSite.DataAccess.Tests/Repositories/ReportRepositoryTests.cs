using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class ReportRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly ReportRepository _reports;
    private readonly DatabaseFixture _database;

    public ReportRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _reports = new ReportRepository(database.DataSource);
    }

    [Fact]
    public async Task GetSummaryAsync_by_client_orders_by_hours_and_carries_colours()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Client);

        //Assert
        summary.GroupBy.Should().Be("client");
        summary.Rows.Select(r => r.Label).Should().Equal("Big Client", "Small Client");
        summary.Rows[0].Key.Should().Be(data.BigClientId.ToString());
        summary.Rows[0].Color.Should().Be("indigo");
        summary.Rows[0].Sublabel.Should().Be("");
        summary.Rows[0].Hours.Should().Be(9.5m);
        summary.Rows[0].BillableHours.Should().Be(7m);
        summary.Rows[0].Amount.Should().Be(400m);
        summary.Rows[0].EntryCount.Should().Be(4);
        summary.Rows[1].Hours.Should().Be(1.25m);
        summary.Rows[1].Amount.Should().Be(0m);
    }

    [Fact]
    public async Task GetSummaryAsync_totals_billable_and_non_billable_hours()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Client);

        //Assert
        summary.TotalHours.Should().Be(10.75m);
        summary.BillableHours.Should().Be(8.25m);
        summary.NonBillableHours.Should().Be(2.5m);
        summary.TotalAmount.Should().Be(400m);
        summary.EntryCount.Should().Be(5);
        summary.From.Should().Be(new DateOnly(2026, 10, 1));
        summary.To.Should().Be(new DateOnly(2026, 11, 30));
    }

    [Fact]
    public async Task GetSummaryAsync_by_project_puts_the_client_in_the_sublabel()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Project);

        //Assert
        summary.Rows.Select(r => r.Label).Should().Equal("Build", "Support", "Pro bono");
        summary.Rows.Select(r => r.Sublabel).Should().Equal("Big Client", "Big Client", "Small Client");
        summary.Rows[0].Key.Should().Be(data.BuildProjectId.ToString());
    }

    [Fact]
    public async Task GetSummaryAsync_by_day_lists_days_with_entries_oldest_first()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Day);

        //Assert
        summary.Rows.Select(r => r.Key).Should().Equal("2026-10-30", "2026-11-02", "2026-11-08", "2026-11-09");
        summary.Rows.Should().OnlyContain(r => r.Label == r.Key && r.Color == "" && r.Sublabel == "");
        summary.Rows[1].Hours.Should().Be(3.25m);
    }

    [Fact]
    public async Task GetSummaryAsync_by_week_keys_each_row_by_its_monday()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Week);

        //Assert
        summary.Rows.Select(r => r.Key).Should().Equal("2026-10-26", "2026-11-02", "2026-11-09");
        summary.Rows.Select(r => r.Label).Should().Equal("Week of 2026-10-26", "Week of 2026-11-02", "Week of 2026-11-09");
        summary.Rows[1].Hours.Should().Be(5.5m); //Monday 2 November and Sunday 8 November
        summary.Rows.Should().OnlyContain(r => r.Color == "");
    }

    [Fact]
    public async Task GetSummaryAsync_by_month_labels_rows_with_the_month_name()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var summary = await SummaryAsync(data.UserId, ReportGroupBy.Month);

        //Assert
        summary.Rows.Select(r => r.Key).Should().Equal("2026-10", "2026-11");
        summary.Rows.Select(r => r.Label).Should().Equal("October 2026", "November 2026");
        summary.Rows[0].Hours.Should().Be(4m);
        summary.Rows[1].Hours.Should().Be(6.75m);
    }

    [Fact]
    public async Task GetSummaryAsync_applies_the_date_client_and_project_filters()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var november = await _reports.GetSummaryAsync(data.UserId, new ReportQuery
        {
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 30),
            GroupBy = ReportGroupBy.Client,
        }, TestContext.Current.CancellationToken);
        var smallClient = await _reports.GetSummaryAsync(data.UserId, new ReportQuery
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 11, 30),
            GroupBy = ReportGroupBy.Project,
            ClientId = data.SmallClientId,
        }, TestContext.Current.CancellationToken);
        var buildOnly = await _reports.GetSummaryAsync(data.UserId, new ReportQuery
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 11, 30),
            GroupBy = ReportGroupBy.Day,
            ProjectId = data.BuildProjectId,
        }, TestContext.Current.CancellationToken);

        //Assert
        november.TotalHours.Should().Be(6.75m);
        smallClient.Rows.Should().ContainSingle().Which.Label.Should().Be("Pro bono");
        buildOnly.TotalHours.Should().Be(6.5m);
        buildOnly.EntryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetSummaryAsync_rounds_amounts_to_cents()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, rate: 33.33m);
        await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, 2), 0.25m);

        //Act
        var summary = await _reports.GetSummaryAsync(user.Id, new ReportQuery
        {
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 30),
            GroupBy = ReportGroupBy.Project,
        }, TestContext.Current.CancellationToken);

        //Assert
        summary.TotalAmount.Should().Be(8.33m);
        summary.Rows[0].Amount.Should().Be(8.33m);
    }

    [Fact]
    public async Task GetExportRowsAsync_lists_entries_oldest_first_with_rates_and_amounts()
    {
        //Arrange
        var data = await SampleAsync();

        //Act
        var rows = await _reports.GetExportRowsAsync(data.UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30),
            null, null, TestContext.Current.CancellationToken);

        //Assert
        rows.Select(r => r.WorkDate).Should().BeInAscendingOrder();
        rows.Should().HaveCount(5);
        var first = rows[0];
        first.WorkDate.Should().Be(new DateOnly(2026, 10, 30));
        first.ClientName.Should().Be("Big Client");
        first.ProjectName.Should().Be("Build");
        first.HourlyRate.Should().Be(100m);
        first.Amount.Should().Be(400m);
        rows.Where(r => r.ProjectName == "Support").Should().OnlyContain(r => r.Amount == null && r.HourlyRate == null);
        rows.Single(r => r.ProjectName == "Build" && !r.IsBillable).Amount.Should().BeNull();
        rows.Single(r => r.ProjectName == "Pro bono").HourlyRate.Should().BeNull();
    }

    [Fact]
    public void MonthLabel_names_the_month_in_english()
        => ReportRepository.MonthLabel("2026-02").Should().Be("February 2026");

    private Task<ReportSummary> SummaryAsync(Guid userId, ReportGroupBy groupBy)
        => _reports.GetSummaryAsync(userId, new ReportQuery
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 11, 30),
            GroupBy = groupBy,
        }, TestContext.Current.CancellationToken);

    /// <summary>
    /// Big Client (indigo): Build at 100/h, 4h billable on Fri 30 Oct and 2.5h non-billable on
    /// Mon 2 Nov; Support without a rate, 0.75h on Mon 2 Nov and 2.25h on Sun 8 Nov, both billable.
    /// Small Client (pink): Pro bono without a rate, 1.25h billable on Mon 9 Nov. One more Build
    /// entry on 1 Dec lies outside the October-November range.
    /// </summary>
    private async Task<(Guid UserId, Guid BigClientId, Guid SmallClientId, Guid BuildProjectId)> SampleAsync()
    {
        var user = await TestData.UserAsync(_database.DataSource);
        var big = await TestData.ClientAsync(_database.DataSource, user.Id, "Big Client", color: "indigo");
        var small = await TestData.ClientAsync(_database.DataSource, user.Id, "Small Client", color: "pink");
        var build = await TestData.ProjectAsync(_database.DataSource, user.Id, big.Id, "Build", rate: 100m);
        var support = await TestData.ProjectAsync(_database.DataSource, user.Id, big.Id, "Support", rate: null);
        var proBono = await TestData.ProjectAsync(_database.DataSource, user.Id, small.Id, "Pro bono", rate: null);
        await TestData.EntryAsync(_database.DataSource, user.Id, build.Id, new DateOnly(2026, 10, 30), 4m);
        await TestData.EntryAsync(_database.DataSource, user.Id, build.Id, new DateOnly(2026, 11, 2), 2.5m, billable: false);
        await TestData.EntryAsync(_database.DataSource, user.Id, support.Id, new DateOnly(2026, 11, 2), 0.75m);
        await TestData.EntryAsync(_database.DataSource, user.Id, support.Id, new DateOnly(2026, 11, 8), 2.25m);
        await TestData.EntryAsync(_database.DataSource, user.Id, proBono.Id, new DateOnly(2026, 11, 9), 1.25m);
        await TestData.EntryAsync(_database.DataSource, user.Id, build.Id, new DateOnly(2026, 12, 1), 8m);
        return (user.Id, big.Id, small.Id, build.Id);
    }
}
