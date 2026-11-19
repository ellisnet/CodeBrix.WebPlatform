using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Auth;

public class AuthCookieTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthCookieTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task sign_in_sets_an_http_only_secure_strict_access_cookie_on_the_api_path()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        await TestUsers.RegisterAsync(client);

        //Assert
        var access = Header(client, "ws_access");
        access.Should().Contain("path=/api;");
        access.Should().Contain("httponly");
        access.Should().Contain("secure");
        access.Should().Contain("samesite=strict");
        ExpiresIn(access).Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task sign_in_sets_an_http_only_secure_strict_refresh_cookie_on_the_auth_path()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        await TestUsers.RegisterAsync(client);

        //Assert
        var refresh = Header(client, "ws_refresh");
        refresh.Should().Contain("path=/api/auth;");
        refresh.Should().Contain("httponly");
        refresh.Should().Contain("secure");
        refresh.Should().Contain("samesite=strict");
        ExpiresIn(refresh).Should().BeCloseTo(TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));
        client.Cookie("ws_refresh").Length.Should().BeGreaterThanOrEqualTo(43);
    }

    [Fact]
    public async Task logout_expires_both_cookies_on_their_paths()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        await client.PostAsync("/api/auth/logout");

        //Assert
        var access = Header(client, "ws_access");
        var refresh = Header(client, "ws_refresh");
        access.Should().Contain("path=/api;");
        refresh.Should().Contain("path=/api/auth;");
        ExpiresIn(access).Should().BeLessThan(TimeSpan.Zero);
        ExpiresIn(refresh).Should().BeLessThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task the_refresh_cookie_is_not_a_stored_value()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var token = client.Cookie("ws_refresh");

        //Act
        await using var connection = new PgSqlConnection(_factory.ConnectionString);
        var stored = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM sessions WHERE encode(refresh_token_hash, 'escape') LIKE '%' || @token || '%'",
            new { token }, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        stored.Should().Be(0);
    }

    private static string Header(ApiClient client, string name)
        => client.LastSetCookies.Single(h => h.StartsWith(name + "=", StringComparison.Ordinal)).ToLowerInvariant() + ";";

    private static TimeSpan ExpiresIn(string header)
    {
        var part = header.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.Ordinal));
        return DateTimeOffset.Parse(part.Substring(8)) - DateTimeOffset.UtcNow;
    }
}
