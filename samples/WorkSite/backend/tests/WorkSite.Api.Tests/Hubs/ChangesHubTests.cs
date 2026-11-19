using System;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hubs;

public class ChangesHubTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ChangesHubTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task creating_changing_and_deleting_a_client_tells_the_users_connections()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/changes", client);

        //Act
        var clientId = await TestUsers.CreateClientAsync(client, "Notice Co");
        var created = await connection.NextAsync("Changed", TimeSpan.FromSeconds(5));
        await client.PutAsync("/api/clients/" + clientId, new { name = "Notice Co 2", contactName = "", contactEmail = "", notes = "", color = "teal" });
        var updated = await connection.NextAsync("Changed", TimeSpan.FromSeconds(5));
        (await client.DeleteAsync("/api/clients/" + clientId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deleted = await connection.NextAsync("Changed", TimeSpan.FromSeconds(5));

        //Assert
        created[0].GetProperty("entity").GetString().Should().Be("Client");
        created[0].GetProperty("kind").GetString().Should().Be("Created");
        created[0].GetProperty("id").GetString().Should().Be(clientId);
        updated[0].GetProperty("kind").GetString().Should().Be("Updated");
        deleted[0].GetProperty("kind").GetString().Should().Be("Deleted");
    }

    [Fact]
    public async Task projects_and_time_entries_are_announced_with_their_own_kind()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var clientId = await TestUsers.CreateClientAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/changes", client);

        //Act
        var projectId = await TestUsers.CreateProjectAsync(client, clientId);
        var project = await connection.NextAsync("Changed", TimeSpan.FromSeconds(5));
        var entryId = await TestUsers.CreateEntryAsync(client, projectId, DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), 1m);
        var entry = await connection.NextAsync("Changed", TimeSpan.FromSeconds(5));

        //Assert
        project[0].GetProperty("entity").GetString().Should().Be("Project");
        project[0].GetProperty("id").GetString().Should().Be(projectId);
        entry[0].GetProperty("entity").GetString().Should().Be("TimeEntry");
        entry[0].GetProperty("id").GetString().Should().Be(entryId);
    }

    [Fact]
    public async Task one_users_changes_are_not_announced_to_another_user()
    {
        //Arrange
        using var writer = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(writer);
        using var bystander = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(bystander);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/changes", bystander);

        //Act
        await TestUsers.CreateClientAsync(writer);

        //Assert
        (await connection.StaysSilentAsync("Changed", TimeSpan.FromSeconds(1.5))).Should().BeTrue();
    }

    [Fact]
    public async Task a_refused_change_is_not_announced()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        await using var connection = await HubTestConnection.ConnectAsync(_factory, "/api/hubs/changes", client);

        //Act
        var response = await client.PostAsync("/api/clients", new { name = "", contactName = "", contactEmail = "", notes = "", color = "teal" });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await connection.StaysSilentAsync("Changed", TimeSpan.FromSeconds(1.5))).Should().BeTrue();
    }
}
