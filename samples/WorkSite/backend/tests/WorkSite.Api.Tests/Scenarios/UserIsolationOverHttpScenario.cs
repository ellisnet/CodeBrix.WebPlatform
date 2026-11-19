using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Scenarios;

/// <summary>Another user's ids behave exactly like ids that do not exist.</summary>
public class UserIsolationOverHttpScenario : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UserIsolationOverHttpScenario(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task another_users_resources_answer_404_and_never_appear_in_lists()
    {
        //Arrange
        using var alice = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(alice, TestUsers.NewUsername("alice"));
        var clientId = await TestUsers.CreateClientAsync(alice, "Alice's client");
        var projectId = await TestUsers.CreateProjectAsync(alice, clientId);
        var entryId = await TestUsers.CreateEntryAsync(alice, projectId, "2026-11-02", 3m);
        using var bob = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(bob, TestUsers.NewUsername("bob"));

        //Act
        var responses = new[]
        {
            await bob.GetAsync("/api/clients/" + clientId),
            await bob.PutAsync("/api/clients/" + clientId, new { name = "Taken over", color = "red" }),
            await bob.DeleteAsync("/api/clients/" + clientId),
            await bob.GetAsync("/api/projects/" + projectId),
            await bob.PutAsync("/api/projects/" + projectId, new { clientId, name = "Taken over" }),
            await bob.DeleteAsync("/api/projects/" + projectId),
            await bob.GetAsync("/api/time-entries/" + entryId),
            await bob.PutAsync("/api/time-entries/" + entryId, new { projectId, workDate = "2026-11-02", hours = 1m }),
            await bob.DeleteAsync("/api/time-entries/" + entryId),
        };
        var bobsClients = await ApiClient.JsonAsync(await bob.GetAsync("/api/clients?includeArchived=true"));
        var bobsProjects = await ApiClient.JsonAsync(await bob.GetAsync("/api/projects?includeArchived=true&clientId=" + clientId));
        var bobsEntries = await ApiClient.JsonAsync(await bob.GetAsync("/api/time-entries?clientId=" + clientId));
        var bobsReport = await ApiClient.JsonAsync(await bob.GetAsync("/api/reports/summary?from=2026-11-01&to=2026-11-30"));
        var bobsExport = await (await bob.GetAsync($"/api/reports/export.csv?from=2026-11-01&to=2026-11-30&clientId={clientId}"))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        //Assert
        foreach (var response in responses)
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }

        bobsClients.GetArrayLength().Should().Be(0);
        bobsProjects.GetArrayLength().Should().Be(0);
        bobsEntries.GetProperty("totalCount").GetInt32().Should().Be(0);
        bobsReport.GetProperty("totalHours").GetDecimal().Should().Be(0m);
        bobsExport.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
        var alicesEntry = await ApiClient.JsonAsync(await alice.GetAsync("/api/time-entries/" + entryId));
        alicesEntry.GetProperty("hours").GetDecimal().Should().Be(3m);
        (await ApiClient.JsonAsync(await alice.GetAsync("/api/clients/" + clientId))).GetProperty("name").GetString()
            .Should().Be("Alice's client");
    }

    [Fact]
    public async Task another_users_client_or_project_cannot_be_referenced()
    {
        //Arrange
        using var alice = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(alice);
        var clientId = await TestUsers.CreateClientAsync(alice);
        var projectId = await TestUsers.CreateProjectAsync(alice, clientId);
        using var bob = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(bob);
        var bobsClient = await TestUsers.CreateClientAsync(bob);
        var bobsProject = await TestUsers.CreateProjectAsync(bob, bobsClient);

        //Act
        var project = await bob.PostAsync("/api/projects", new { clientId, name = "Sneaky" });
        var move = await bob.PutAsync("/api/projects/" + bobsProject, new { clientId, name = "Sneaky" });
        var entry = await bob.PostAsync("/api/time-entries", new { projectId, workDate = "2026-11-02", hours = 1m });

        //Assert
        (await project.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("clientId");
        (await move.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("clientId");
        (await entry.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).ErrorFields().Should().Equal("projectId");
        var alicesProjects = await ApiClient.JsonAsync(await alice.GetAsync("/api/projects"));
        alicesProjects.EnumerateArray().Select(p => p.GetProperty("id").GetString()).Should().Equal(projectId);
    }
}
