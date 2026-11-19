using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class ReportsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task summary_by_client_returns_the_contract_shape()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var (clientId, _) = await SampleAsync(client);

        //Act
        var response = await client.GetAsync("/api/reports/summary?from=2026-10-01&to=2026-11-30&groupBy=client");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("from", "to", "groupBy", "totalHours",
            "billableHours", "nonBillableHours", "totalAmount", "entryCount", "rows");
        body.GetProperty("from").GetString().Should().Be("2026-10-01");
        body.GetProperty("to").GetString().Should().Be("2026-11-30");
        body.GetProperty("groupBy").GetString().Should().Be("client");
        body.GetProperty("totalHours").GetDecimal().Should().Be(6.5m);
        body.GetProperty("billableHours").GetDecimal().Should().Be(4.5m);
        body.GetProperty("nonBillableHours").GetDecimal().Should().Be(2m);
        body.GetProperty("totalAmount").GetDecimal().Should().Be(675m);
        body.GetProperty("entryCount").GetInt32().Should().Be(3);
        var row = body.GetProperty("rows")[0];
        row.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("key", "label", "sublabel", "color", "hours",
            "billableHours", "amount", "entryCount");
        row.GetProperty("key").GetString().Should().Be(clientId);
        row.GetProperty("label").GetString().Should().Be("Report Co");
        row.GetProperty("color").GetString().Should().Be("indigo");
    }

    [Theory]
    [InlineData("project", "Report work", "Report Co")]
    [InlineData("day", "2026-10-30", "")]
    [InlineData("week", "Week of 2026-10-26", "")]
    [InlineData("month", "October 2026", "")]
    public async Task summary_labels_the_first_row_for_each_grouping(string groupBy, string label, string sublabel)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await SampleAsync(client);

        //Act
        var body = await ApiClient.JsonAsync(await client.GetAsync(
            $"/api/reports/summary?from=2026-10-01&to=2026-11-30&groupBy={groupBy}"));

        //Assert
        body.GetProperty("groupBy").GetString().Should().Be(groupBy);
        body.GetProperty("rows")[0].GetProperty("label").GetString().Should().Be(label);
        body.GetProperty("rows")[0].GetProperty("sublabel").GetString().Should().Be(sublabel);
    }

    [Fact]
    public async Task summary_defaults_to_the_current_month_grouped_by_client()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = new DateOnly(today.Year, today.Month, 1);

        //Act
        var body = await ApiClient.JsonAsync(await client.GetAsync("/api/reports/summary"));

        //Assert
        body.GetProperty("from").GetString().Should().Be(first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        body.GetProperty("to").GetString().Should().Be(first.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        body.GetProperty("groupBy").GetString().Should().Be("client");
        body.GetProperty("rows").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("groupBy=year", "groupBy")]
    [InlineData("groupBy=1", "groupBy")]
    [InlineData("from=2026-11-30&to=2026-11-01", "to")]
    [InlineData("from=nope", "from")]
    public async Task summary_rejects_invalid_parameters(string query, string field)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.GetAsync("/api/reports/summary?" + query);

        //Assert
        (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal(field);
    }

    [Fact]
    public async Task export_returns_a_csv_attachment_with_the_contract_columns()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await SampleAsync(client);

        //Act
        var response = await client.GetAsync("/api/reports/export.csv?from=2026-10-01&to=2026-11-30");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition.ToString()
            .Should().Be("attachment; filename=\"worksite-time-2026-10-01-to-2026-11-30.csv\"");
        var csv = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var lines = csv.Split("\r\n");
        lines[0].Should().Be("Date,Client,Project,Hours,Billable,Rate,Amount,Description");
        lines[1].Should().Be("2026-10-30,Report Co,Report work,4.00,Yes,150.00,600.00,\"Design, review\"");
        lines[2].Should().Be("2026-11-02,Report Co,Report work,2.00,No,150.00,,'=SUM(A1)");
        lines[3].Should().Be("2026-11-09,Report Co,Report work,0.50,Yes,150.00,75.00,\"Said \"\"hello\"\"\"");
        lines[4].Should().Be("");
    }

    [Fact]
    public async Task export_with_no_entries_has_only_the_header()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.GetAsync("/api/reports/export.csv?from=2020-01-01&to=2020-01-31");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be("Date,Client,Project,Hours,Billable,Rate,Amount,Description\r\n");
    }

    [Fact]
    public async Task export_filters_by_project()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var (clientId, projectId) = await SampleAsync(client);
        var other = await TestUsers.CreateProjectAsync(client, clientId, "Other work");
        await TestUsers.CreateEntryAsync(client, other, "2026-11-03", 1m);

        //Act
        var csv = await (await client.GetAsync($"/api/reports/export.csv?from=2026-10-01&to=2026-11-30&projectId={projectId}"))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        //Assert
        csv.Should().NotContain("Other work");
        csv.Split("\r\n").Should().HaveCount(5);
    }

    private static async Task<(string ClientId, string ProjectId)> SampleAsync(ApiClient client)
    {
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client, "Report Co", "indigo");
        var projectId = await TestUsers.CreateProjectAsync(client, clientId, "Report work", hourlyRate: 150m);
        await TestUsers.CreateEntryAsync(client, projectId, "2026-10-30", 4m, "Design, review");
        await TestUsers.CreateEntryAsync(client, projectId, "2026-11-02", 2m, "=SUM(A1)", isBillable: false);
        await TestUsers.CreateEntryAsync(client, projectId, "2026-11-09", 0.5m, "Said \"hello\"");
        return (clientId, projectId);
    }
}
