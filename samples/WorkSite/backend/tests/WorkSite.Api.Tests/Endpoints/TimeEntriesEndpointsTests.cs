using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class TimeEntriesEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimeEntriesEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task post_creates_an_entry_and_answers_201_with_the_contract_shape()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client, "Riverbend");
        var projectId = await TestUsers.CreateProjectAsync(client, clientId, "Reminders");

        //Act
        var response = await client.PostAsync("/api/time-entries",
            new { projectId, workDate = "2026-11-16", hours = 7.5m, description = "Templates", isBillable = true });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "projectId", "projectName", "clientId",
            "clientName", "workDate", "hours", "description", "isBillable", "createdAt", "updatedAt");
        body.GetProperty("workDate").GetString().Should().Be("2026-11-16");
        body.GetProperty("hours").GetDecimal().Should().Be(7.5m);
        body.GetProperty("projectName").GetString().Should().Be("Reminders");
        body.GetProperty("clientId").GetString().Should().Be(clientId);
        body.GetProperty("clientName").GetString().Should().Be("Riverbend");
        body.GetProperty("updatedAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task post_takes_the_billable_default_from_the_project()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var internalWork = await TestUsers.CreateProjectAsync(client, clientId, isBillable: false);

        //Act
        var response = await client.PostAsync("/api/time-entries", new { projectId = internalWork, workDate = "2026-11-16", hours = 1m });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("isBillable").GetBoolean().Should().BeFalse();
        body.GetProperty("description").GetString().Should().Be("");
    }

    [Theory]
    [InlineData(0, "hours")]
    [InlineData(-1, "hours")]
    [InlineData(24.01, "hours")]
    [InlineData(1.125, "hours")]
    public async Task post_rejects_hours_outside_the_contract(double hours, string field)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client));

        //Act
        var response = await client.PostAsync("/api/time-entries",
            new { projectId, workDate = "2026-11-16", hours = (decimal)hours });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal(field);
    }

    [Fact]
    public async Task post_accepts_twenty_four_hours_and_a_date_one_day_ahead()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client));
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        //Act
        var response = await client.PostAsync("/api/time-entries", new { projectId, workDate = tomorrow, hours = 24m });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task post_rejects_a_date_two_days_ahead_a_long_description_and_missing_fields()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client));
        var later = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        //Act
        var future = await client.PostAsync("/api/time-entries",
            new { projectId, workDate = later, hours = 1m, description = new string('d', 501) });
        var empty = await client.PostAsync("/api/time-entries", new { });

        //Assert
        (await future.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed"))
            .ErrorFields().Should().Equal("description", "workDate");
        (await empty.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed"))
            .ErrorFields().Should().Equal("hours", "projectId", "workDate");
    }

    [Fact]
    public async Task post_rejects_a_malformed_date_or_number_as_validation_failed()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client));

        //Act
        var badDate = await client.PostAsync("/api/time-entries", new { projectId, workDate = "16/11/2026", hours = 1m });
        var badHours = await client.PostAsync("/api/time-entries", new { projectId, workDate = "2026-11-16", hours = "lots" });

        //Assert
        (await badDate.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("workDate");
        (await badHours.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("hours");
    }

    [Fact]
    public async Task post_refuses_an_archived_project_or_a_project_of_an_archived_client()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var archived = await TestUsers.CreateProjectAsync(client, clientId, "Archived");
        await client.PutAsync("/api/projects/" + archived, new { clientId, name = "Archived", isArchived = true });
        var archivedClient = await TestUsers.CreateClientAsync(client, "Gone");
        var underArchivedClient = await TestUsers.CreateProjectAsync(client, archivedClient);
        await client.PutAsync("/api/clients/" + archivedClient, new { name = "Gone", color = "red", isArchived = true });

        //Act
        var first = await client.PostAsync("/api/time-entries", new { projectId = archived, workDate = "2026-11-16", hours = 1m });
        var second = await client.PostAsync("/api/time-entries",
            new { projectId = underArchivedClient, workDate = "2026-11-16", hours = 1m });

        //Assert
        (await first.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("projectId");
        (await second.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("projectId");
    }

    [Fact]
    public async Task get_pages_filters_and_totals_the_whole_filtered_set()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientA = await TestUsers.CreateClientAsync(client);
        var clientB = await TestUsers.CreateClientAsync(client);
        var projectA = await TestUsers.CreateProjectAsync(client, clientA);
        var projectB = await TestUsers.CreateProjectAsync(client, clientB);
        for (var day = 1; day <= 9; day++)
        {
            await TestUsers.CreateEntryAsync(client, day <= 5 ? projectA : projectB, $"2026-10-0{day}", 2m, isBillable: day % 3 != 0);
        }

        //Act
        var page = await ApiClient.JsonAsync(await client.GetAsync("/api/time-entries?page=2&pageSize=4"));
        var defaults = await ApiClient.JsonAsync(await client.GetAsync("/api/time-entries"));
        var filtered = await ApiClient.JsonAsync(await client.GetAsync(
            $"/api/time-entries?from=2026-10-02&to=2026-10-08&clientId={clientA}&billable=true"));
        var byProject = await ApiClient.JsonAsync(await client.GetAsync($"/api/time-entries?projectId={projectB}&billable=false"));

        //Assert
        page.GetProperty("page").GetInt32().Should().Be(2);
        page.GetProperty("pageSize").GetInt32().Should().Be(4);
        page.GetProperty("totalCount").GetInt32().Should().Be(9);
        page.GetProperty("totalHours").GetDecimal().Should().Be(18m);
        page.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("workDate").GetString())
            .Should().Equal("2026-10-05", "2026-10-04", "2026-10-03", "2026-10-02");
        defaults.GetProperty("page").GetInt32().Should().Be(1);
        defaults.GetProperty("pageSize").GetInt32().Should().Be(25);
        filtered.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("workDate").GetString())
            .Should().Equal("2026-10-05", "2026-10-04", "2026-10-02");
        filtered.GetProperty("totalHours").GetDecimal().Should().Be(6m);
        byProject.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("workDate").GetString())
            .Should().Equal("2026-10-09", "2026-10-06");
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("from=2026-10-09&to=2026-10-01", "to")]
    [InlineData("from=yesterday", "from")]
    [InlineData("billable=maybe", "billable")]
    [InlineData("clientId=not-a-guid", "clientId")]
    public async Task get_rejects_invalid_filters(string query, string field)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.GetAsync("/api/time-entries?" + query);

        //Assert
        (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal(field);
    }

    [Fact]
    public async Task put_replaces_an_entry_and_keeps_billable_when_absent()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, clientId);
        var id = await TestUsers.CreateEntryAsync(client, projectId, "2026-11-02", 1m, isBillable: false);

        //Act
        var response = await client.PutAsync("/api/time-entries/" + id,
            new { projectId, workDate = "2026-11-03", hours = 2.75m, description = "Changed" });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("workDate").GetString().Should().Be("2026-11-03");
        body.GetProperty("hours").GetDecimal().Should().Be(2.75m);
        body.GetProperty("description").GetString().Should().Be("Changed");
        body.GetProperty("isBillable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task put_allows_correcting_an_entry_on_an_archived_project_but_not_moving_one_there()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var archived = await TestUsers.CreateProjectAsync(client, clientId, "Archived later");
        var active = await TestUsers.CreateProjectAsync(client, clientId, "Active");
        var onArchived = await TestUsers.CreateEntryAsync(client, archived, "2026-11-02", 1m);
        var onActive = await TestUsers.CreateEntryAsync(client, active, "2026-11-02", 1m);
        await client.PutAsync("/api/projects/" + archived, new { clientId, name = "Archived later", isArchived = true });

        //Act
        var correction = await client.PutAsync("/api/time-entries/" + onArchived,
            new { projectId = archived, workDate = "2026-11-02", hours = 1.5m });
        var move = await client.PutAsync("/api/time-entries/" + onActive,
            new { projectId = archived, workDate = "2026-11-02", hours = 1m });

        //Assert
        correction.StatusCode.Should().Be(HttpStatusCode.OK);
        (await move.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("projectId");
    }

    [Fact]
    public async Task get_put_and_delete_by_id()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var projectId = await TestUsers.CreateProjectAsync(client, await TestUsers.CreateClientAsync(client));
        var id = await TestUsers.CreateEntryAsync(client, projectId, "2026-11-02", 1m);

        //Act
        var found = await client.GetAsync("/api/time-entries/" + id);
        var deleted = await client.DeleteAsync("/api/time-entries/" + id);
        var gone = await client.GetAsync("/api/time-entries/" + id);
        var putGone = await client.PutAsync("/api/time-entries/" + id, new { projectId, workDate = "2026-11-02", hours = 1m });
        var deleteGone = await client.DeleteAsync("/api/time-entries/" + id);

        //Assert
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await gone.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await putGone.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await deleteGone.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }
}
