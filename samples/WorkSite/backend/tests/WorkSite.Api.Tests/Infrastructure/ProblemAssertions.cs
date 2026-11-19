using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>Checks that a response carries the contract's error body.</summary>
public static class ProblemAssertions
{
    /// <summary>Asserts the status, the content type and the body's status, code and title.</summary>
    /// <param name="response">The response.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    /// <returns>The body, for further checks.</returns>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        return body;
    }

    /// <summary>The field names in a problem's <c>errors</c>.</summary>
    /// <param name="problem">The body.</param>
    /// <returns>The names, sorted.</returns>
    public static string[] ErrorFields(this JsonElement problem)
        => problem.TryGetProperty("errors", out var errors)
            ? errors.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>The first message for a field in a problem's <c>errors</c>.</summary>
    /// <param name="problem">The body.</param>
    /// <param name="field">The field.</param>
    /// <returns>The message.</returns>
    public static string FirstError(this JsonElement problem, string field)
        => problem.GetProperty("errors").GetProperty(field)[0].GetString();

    /// <summary>Asserts an empty 401.</summary>
    /// <param name="response">The response.</param>
    /// <returns>A task that completes when checked.</returns>
    public static async Task ShouldBeEmptyUnauthorizedAsync(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }
}
