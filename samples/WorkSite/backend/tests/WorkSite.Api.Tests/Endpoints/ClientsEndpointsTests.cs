using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Endpoints;

public class ClientsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClientsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task post_creates_a_client_and_answers_201_with_the_contract_shape()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostAsync("/api/clients", new
        {
            name = "  Harbor Light  ",
            contactName = "Maya Chen",
            contactEmail = "maya@example.com",
            notes = "Invoices monthly.",
            color = "amber",
        });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        response.Headers.Location.ToString().Should().Be("/api/clients/" + body.GetProperty("id").GetString());
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "id", "name", "contactName", "contactEmail", "notes", "color", "isArchived", "createdAt", "projectCount", "totalHours");
        body.GetProperty("name").GetString().Should().Be("Harbor Light");
        body.GetProperty("color").GetString().Should().Be("amber");
        body.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        body.GetProperty("projectCount").GetInt32().Should().Be(0);
        body.GetProperty("totalHours").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task post_treats_missing_optional_text_as_empty_strings()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostAsync("/api/clients", new { name = "Minimal", color = "slate" });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("contactName").GetString().Should().Be("");
        body.GetProperty("contactEmail").GetString().Should().Be("");
        body.GetProperty("notes").GetString().Should().Be("");
    }

    [Fact]
    public async Task post_validates_name_email_and_colour()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostAsync("/api/clients",
            new { name = new string('n', 121), contactEmail = "nope", color = "blue" });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("color", "contactEmail", "name");
    }

    [Fact]
    public async Task post_answers_409_for_a_name_already_used_ignoring_case()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await TestUsers.CreateClientAsync(client, "Duplicate Co");

        //Act
        var response = await client.PostAsync("/api/clients", new { name = "duplicate co", color = "red" });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        problem.ErrorFields().Should().Equal("name");
    }

    [Fact]
    public async Task get_lists_clients_by_name_and_hides_archived_ones_unless_asked()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await TestUsers.CreateClientAsync(client, "Zulu");
        var archivedId = await TestUsers.CreateClientAsync(client, "Mike");
        await TestUsers.CreateClientAsync(client, "alpha");
        await client.PutAsync("/api/clients/" + archivedId, new { name = "Mike", color = "teal", isArchived = true });

        //Act
        var active = await ApiClient.JsonAsync(await client.GetAsync("/api/clients"));
        var explicitFalse = await ApiClient.JsonAsync(await client.GetAsync("/api/clients?includeArchived=false"));
        var all = await ApiClient.JsonAsync(await client.GetAsync("/api/clients?includeArchived=true"));

        //Assert
        active.EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Equal("alpha", "Zulu");
        explicitFalse.GetArrayLength().Should().Be(2);
        all.EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Equal("alpha", "Mike", "Zulu");
    }

    [Fact]
    public async Task get_by_id_answers_200_or_404()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var id = await TestUsers.CreateClientAsync(client, "Findable");

        //Act
        var found = await client.GetAsync("/api/clients/" + id);
        var missing = await client.GetAsync("/api/clients/" + Guid.NewGuid());

        //Assert
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiClient.JsonAsync(found)).GetProperty("name").GetString().Should().Be("Findable");
        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task put_replaces_values_and_keeps_the_archived_flag_when_absent()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var id = await TestUsers.CreateClientAsync(client, "Before");
        await client.PutAsync("/api/clients/" + id, new { name = "Before", color = "teal", isArchived = true });

        //Act
        var response = await client.PutAsync("/api/clients/" + id,
            new { name = "After", contactName = "Sam", contactEmail = "", notes = "n", color = "violet" });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("name").GetString().Should().Be("After");
        body.GetProperty("contactName").GetString().Should().Be("Sam");
        body.GetProperty("color").GetString().Should().Be("violet");
        body.GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task put_answers_404_for_an_unknown_client_and_409_for_a_taken_name()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await TestUsers.CreateClientAsync(client, "Taken");
        var id = await TestUsers.CreateClientAsync(client, "Mine");

        //Act
        var unknown = await client.PutAsync("/api/clients/" + Guid.NewGuid(), new { name = "X", color = "red" });
        var taken = await client.PutAsync("/api/clients/" + id, new { name = "TAKEN", color = "red" });

        //Assert
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task delete_answers_204_then_404_and_409_while_projects_remain()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var empty = await TestUsers.CreateClientAsync(client);
        var busy = await TestUsers.CreateClientAsync(client);
        await TestUsers.CreateProjectAsync(client, busy);

        //Act
        var deleted = await client.DeleteAsync("/api/clients/" + empty);
        var again = await client.DeleteAsync("/api/clients/" + empty);
        var blocked = await client.DeleteAsync("/api/clients/" + busy);

        //Assert
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        var problem = await blocked.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        problem.GetProperty("title").GetString().Should().Contain("Archive it instead.");
    }

    [Fact]
    public async Task totals_count_projects_and_hours()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var id = await TestUsers.CreateClientAsync(client);
        var project = await TestUsers.CreateProjectAsync(client, id);
        await TestUsers.CreateProjectAsync(client, id);
        await TestUsers.CreateEntryAsync(client, project, "2026-11-02", 2.25m);

        //Act
        var body = await ApiClient.JsonAsync(await client.GetAsync("/api/clients/" + id));

        //Assert
        body.GetProperty("projectCount").GetInt32().Should().Be(2);
        body.GetProperty("totalHours").GetDecimal().Should().Be(2.25m);
    }
}
