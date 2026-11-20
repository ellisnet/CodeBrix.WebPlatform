using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using WorkSite.Api;
using Xunit;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>
/// Talks to the API the way the browser frontend does: it keeps the cookies the server sets,
/// sends each cookie only to its path, and adds <c>X-Requested-With: WorkSite</c> to state-changing
/// requests unless told not to. Tests can read and replace the cookies directly.
/// </summary>
public sealed class ApiClient : IDisposable
{
    /// <summary>The backend's declared request marker, which the frontend sends in X-Requested-With.</summary>
    private static readonly string RequestMarker = WireRequestMarkerAttribute.GetValue(typeof(Program).Assembly);

    private readonly HttpClient _http;
    private readonly Dictionary<string, (string Value, string Path)> _cookies = new(StringComparer.Ordinal);

    /// <summary>Wraps a test-server client that does not handle cookies itself.</summary>
    /// <param name="http">The client.</param>
    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>The Set-Cookie headers of the last response.</summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    /// <summary>The current value of a cookie, or null.</summary>
    /// <param name="name">The cookie name.</param>
    /// <returns>The value, or null when the jar does not hold it.</returns>
    public string Cookie(string name) => _cookies.TryGetValue(name, out var cookie) ? cookie.Value : null;

    /// <summary>Puts a cookie in the jar, replacing any with that name.</summary>
    /// <param name="name">The cookie name.</param>
    /// <param name="value">The value.</param>
    /// <param name="path">The path the cookie is sent to.</param>
    public void SetCookie(string name, string value, string path) => _cookies[name] = (value, path);

    /// <summary>The <c>Cookie</c> header a browser would send to a path, or null when it would send none.</summary>
    /// <param name="path">The request path.</param>
    /// <returns>The header value.</returns>
    public string CookieHeader(string path)
    {
        var cookies = _cookies.Where(c => path.StartsWith(c.Value.Path, StringComparison.Ordinal))
            .Select(c => c.Key + "=" + c.Value.Value).ToList();
        return cookies.Count == 0 ? null : string.Join("; ", cookies);
    }

    /// <summary>Removes every cookie.</summary>
    public void ClearCookies() => _cookies.Clear();

    /// <summary>GET.</summary>
    /// <param name="path">The path and query.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

    /// <summary>POST a JSON body (or no body when <paramref name="body"/> is null).</summary>
    /// <param name="path">The path.</param>
    /// <param name="body">The body, serialised with camelCase names.</param>
    /// <param name="withRequestedWith">Whether to send X-Requested-With.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> PostAsync(string path, object body = null, bool withRequestedWith = true)
        => SendAsync(WithBody(HttpMethod.Post, path, body), withRequestedWith);

    /// <summary>PUT a JSON body.</summary>
    /// <param name="path">The path.</param>
    /// <param name="body">The body.</param>
    /// <param name="withRequestedWith">Whether to send X-Requested-With.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> PutAsync(string path, object body, bool withRequestedWith = true)
        => SendAsync(WithBody(HttpMethod.Put, path, body), withRequestedWith);

    /// <summary>DELETE.</summary>
    /// <param name="path">The path.</param>
    /// <param name="withRequestedWith">Whether to send X-Requested-With.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> DeleteAsync(string path, bool withRequestedWith = true)
        => SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), withRequestedWith);

    /// <summary>POST raw text with a given content type.</summary>
    /// <param name="path">The path.</param>
    /// <param name="text">The body.</param>
    /// <param name="contentType">The media type.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> PostRawAsync(string path, string text, string contentType)
        => SendAsync(new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(text, Encoding.UTF8, contentType),
        }, withRequestedWith: true);

    /// <summary>Sends a request with the jar's cookies and stores the cookies of the response.</summary>
    /// <param name="request">The request.</param>
    /// <param name="withRequestedWith">Whether to send X-Requested-With.</param>
    /// <returns>The response.</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool withRequestedWith = false)
    {
        if (withRequestedWith)
        {
            request.Headers.Add(WireRequestMarkerAttribute.HeaderName, RequestMarker);
        }

        var path = request.RequestUri.IsAbsoluteUri ? request.RequestUri.AbsolutePath : "/" + request.RequestUri.OriginalString.TrimStart('/');
        var cookies = _cookies.Where(c => path.StartsWith(c.Value.Path, StringComparison.Ordinal))
            .Select(c => c.Key + "=" + c.Value.Value).ToList();
        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }

        var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        foreach (var header in LastSetCookies)
        {
            Store(header);
        }

        return response;
    }

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The root element.</returns>
    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static HttpRequestMessage WithBody(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        return request;
    }

    private void Store(string setCookie)
    {
        var parts = setCookie.Split(';').Select(p => p.Trim()).ToList();
        var equals = parts[0].IndexOf('=');
        var name = parts[0].Substring(0, equals);
        var value = parts[0].Substring(equals + 1);
        var path = parts.Skip(1).FirstOrDefault(p => p.StartsWith("path=", StringComparison.OrdinalIgnoreCase))?.Substring(5) ?? "/";
        var expires = parts.Skip(1).FirstOrDefault(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        var expired = expires != null && DateTimeOffset.TryParse(expires.Substring(8), out var at) && at < DateTimeOffset.UtcNow;
        if (expired || value.Length == 0)
        {
            _cookies.Remove(name);
        }
        else
        {
            _cookies[name] = (value, path);
        }
    }
}
