using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>Sending requests and checking answers.</summary>
public static class Responses
{
    public static Task<HttpResponseMessage> SendAsync(this HttpClient client, string method, string path,
        bool withMarker = false, string origin = null, string json = null, string contentType = "application/json")
    {
        //An absolute address, so that a path that starts with "//" stays a path (not a host).
        var address = path.StartsWith('/') ? new Uri(BackendHost.OwnOrigin + path) : new Uri(path, UriKind.RelativeOrAbsolute);
        var request = new HttpRequestMessage(new HttpMethod(method), address);
        if (withMarker)
        {
            request.Headers.Add(WireRequestMarkerAttribute.HeaderName, SampleApplication.RequestMarker);
        }

        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<string> TextAsync(this HttpResponseMessage response)
        => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    /// <summary>Asserts the status, the content type and the body's status, code and title.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType.MediaType.Should().Be("application/problem+json");
        var text = await response.TextAsync();
        text.Should().NotContain("<html");
        var body = JsonDocument.Parse(text).RootElement.Clone();
        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        return body;
    }

    /// <summary>Asserts an HTML answer that is one particular index.html.</summary>
    public static async Task ShouldBeIndexAsync(this HttpResponseMessage response, string expectedContent)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        (await response.TextAsync()).Should().Be(expectedContent);
    }

    /// <summary>Asserts a 404 that is not an index.html.</summary>
    public static async Task ShouldBePlainNotFoundAsync(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.TextAsync()).Should().NotContain("<html");
    }

    /// <summary>The field names in a problem's errors, sorted.</summary>
    public static string[] ErrorFields(this JsonElement problem)
        => problem.TryGetProperty("errors", out var errors)
            ? errors.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    /// <summary>A header of the answer (response or content headers), or null.</summary>
    public static string Header(this HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values)
            : response.Content.Headers.TryGetValues(name, out var contentValues) ? string.Join(",", contentValues) : null;
}
