using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class ProjectsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task post_creates_a_project_and_answers_201_with_the_contract_shape()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client, "Cedar");

        //Act
        var response = await client.PostAsync("/api/projects", new
        {
            clientId,
            name = "Portal",
            description = "Client portal",
            hourlyRate = 140.5m,
            isBillable = true,
        });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "id", "clientId", "clientName", "name", "description", "hourlyRate", "isBillable", "isArchived", "createdAt", "totalHours");
        body.GetProperty("clientId").GetString().Should().Be(clientId);
        body.GetProperty("clientName").GetString().Should().Be("Cedar");
        body.GetProperty("hourlyRate").GetDecimal().Should().Be(140.5m);
        body.GetProperty("isBillable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task post_accepts_a_null_rate_and_defaults_billable_to_true()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);

        //Act
        var response = await client.PostAsync("/api/projects", new { clientId, name = "No rate", hourlyRate = (decimal?)null });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("hourlyRate").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("isBillable").GetBoolean().Should().BeTrue();
        body.GetProperty("description").GetString().Should().Be("");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000.01)]
    [InlineData(12.345)]
    public async Task post_rejects_a_rate_outside_the_contract(double rate)
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);

        //Act
        var response = await client.PostAsync("/api/projects", new { clientId, name = "Rated", hourlyRate = (decimal)rate });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("hourlyRate");
    }

    [Fact]
    public async Task post_requires_one_of_the_users_clients()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var missing = await client.PostAsync("/api/projects", new { name = "Orphan" });
        var unknown = await client.PostAsync("/api/projects", new { clientId = Guid.NewGuid(), name = "Orphan" });

        //Assert
        (await missing.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("clientId");
        (await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("clientId");
    }

    [Fact]
    public async Task post_answers_409_for_a_name_the_client_already_has()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var otherClientId = await TestUsers.CreateClientAsync(client);
        await TestUsers.CreateProjectAsync(client, clientId, "Website");

        //Act
        var duplicate = await client.PostAsync("/api/projects", new { clientId, name = "WEBSITE" });
        var elsewhere = await client.PostAsync("/api/projects", new { clientId = otherClientId, name = "Website" });

        //Assert
        (await duplicate.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict")).ErrorFields().Should().Equal("name");
        elsewhere.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task get_orders_by_client_then_name_and_filters_by_client_and_archive()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var beta = await TestUsers.CreateClientAsync(client, "Beta");
        var alpha = await TestUsers.CreateClientAsync(client, "Alpha");
        await TestUsers.CreateProjectAsync(client, beta, "Two");
        await TestUsers.CreateProjectAsync(client, beta, "One");
        var old = await TestUsers.CreateProjectAsync(client, alpha, "Old");
        await TestUsers.CreateProjectAsync(client, alpha, "New");
        await client.PutAsync("/api/projects/" + old, new { clientId = alpha, name = "Old", isArchived = true });

        //Act
        var active = await ApiClient.JsonAsync(await client.GetAsync("/api/projects?clientId=&includeArchived=false"));
        var all = await ApiClient.JsonAsync(await client.GetAsync("/api/projects?includeArchived=true"));
        var betaOnly = await ApiClient.JsonAsync(await client.GetAsync("/api/projects?clientId=" + beta));

        //Assert
        active.EnumerateArray().Select(p => p.GetProperty("name").GetString()).Should().Equal("New", "One", "Two");
        all.EnumerateArray().Select(p => p.GetProperty("name").GetString()).Should().Equal("New", "Old", "One", "Two");
        betaOnly.EnumerateArray().Select(p => p.GetProperty("clientName").GetString()).Should().OnlyContain(n => n == "Beta");
    }

    [Fact]
    public async Task put_replaces_values_and_keeps_the_archived_flag_when_absent()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var id = await TestUsers.CreateProjectAsync(client, clientId, "Original");
        await client.PutAsync("/api/projects/" + id, new { clientId, name = "Original", isArchived = true });

        //Act
        var response = await client.PutAsync("/api/projects/" + id,
            new { clientId, name = "Renamed", description = "d", hourlyRate = 90m, isBillable = false });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("name").GetString().Should().Be("Renamed");
        body.GetProperty("hourlyRate").GetDecimal().Should().Be(90m);
        body.GetProperty("isBillable").GetBoolean().Should().BeFalse();
        body.GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task get_put_and_delete_answer_404_for_an_unknown_project()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var unknown = "/api/projects/" + Guid.NewGuid();

        //Act
        var get = await client.GetAsync(unknown);
        var put = await client.PutAsync(unknown, new { clientId, name = "X" });
        var delete = await client.DeleteAsync(unknown);

        //Assert
        await get.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await put.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await delete.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task delete_answers_409_while_time_entries_remain_and_204_otherwise()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        var used = await TestUsers.CreateProjectAsync(client, clientId);
        var unused = await TestUsers.CreateProjectAsync(client, clientId);
        await TestUsers.CreateEntryAsync(client, used, "2026-11-03", 1m);

        //Act
        var blocked = await client.DeleteAsync("/api/projects/" + used);
        var deleted = await client.DeleteAsync("/api/projects/" + unused);

        //Assert
        var problem = await blocked.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        problem.GetProperty("title").GetString().Should().Contain("Archive it instead.");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
