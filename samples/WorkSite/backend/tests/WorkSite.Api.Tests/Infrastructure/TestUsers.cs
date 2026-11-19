using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using SilverAssertions;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>Registers users and creates their data over HTTP.</summary>
public static class TestUsers
{
    /// <summary>The password every test user gets.</summary>
    public const string Password = "correct horse battery";

    /// <summary>A unique username.</summary>
    /// <param name="prefix">The start of the name.</param>
    /// <returns>The username.</returns>
    public static string NewUsername(string prefix = "user") => prefix + "_" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Registers a new user, leaving the client signed in.</summary>
    /// <param name="client">The client.</param>
    /// <param name="username">The username, or null for a unique one.</param>
    /// <returns>The User body.</returns>
    public static async Task<JsonElement> RegisterAsync(ApiClient client, string username = null)
    {
        username ??= NewUsername();
        var response = await client.PostAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "Person",
            email = username + "@example.com",
            username,
            password = Password,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ApiClient.JsonAsync(response);
    }

    /// <summary>Creates a client and returns its id.</summary>
    /// <param name="client">A signed-in client.</param>
    /// <param name="name">The name, or null for a unique one.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The new client's id.</returns>
    public static async Task<string> CreateClientAsync(ApiClient client, string name = null, string color = "teal")
    {
        var response = await client.PostAsync("/api/clients", new
        {
            name = name ?? "Client " + Guid.NewGuid().ToString("N")[..6],
            contactName = "",
            contactEmail = "",
            notes = "",
            color,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.JsonAsync(response)).GetProperty("id").GetString();
    }

    /// <summary>Creates a project and returns its id.</summary>
    /// <param name="client">A signed-in client.</param>
    /// <param name="clientId">The client id.</param>
    /// <param name="name">The name, or null for a unique one.</param>
    /// <param name="hourlyRate">The rate.</param>
    /// <param name="isBillable">The default billable flag.</param>
    /// <returns>The new project's id.</returns>
    public static async Task<string> CreateProjectAsync(ApiClient client, string clientId, string name = null,
        decimal? hourlyRate = 100m, bool isBillable = true)
    {
        var response = await client.PostAsync("/api/projects", new
        {
            clientId,
            name = name ?? "Project " + Guid.NewGuid().ToString("N")[..6],
            description = "",
            hourlyRate,
            isBillable,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.JsonAsync(response)).GetProperty("id").GetString();
    }

    /// <summary>Creates a time entry and returns its id.</summary>
    /// <param name="client">A signed-in client.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="workDate">The work date, yyyy-MM-dd.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="description">The description.</param>
    /// <param name="isBillable">Whether billable.</param>
    /// <returns>The new entry's id.</returns>
    public static async Task<string> CreateEntryAsync(ApiClient client, string projectId, string workDate, decimal hours,
        string description = "Work", bool isBillable = true)
    {
        var response = await client.PostAsync("/api/time-entries", new { projectId, workDate, hours, description, isBillable });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.JsonAsync(response)).GetProperty("id").GetString();
    }
}
