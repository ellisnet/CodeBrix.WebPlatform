using System;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>
/// PlatformBackendOptions.AdditionalHubOrigins: with the hub Origin check on, a hub request from
/// a listed origin (matched on scheme, host and port, without regard to case) is accepted as
/// one from the site's own pages; every other origin is still refused with 403
/// forbidden_origin; an entry that is not an origin stops the start; with the check off the
/// list is ignored.
/// </summary>
public class AdditionalHubOriginsTests
{
    private const string Negotiate = "/api/hubs/echo/negotiate?negotiateVersion=1";

    private static Task<BackendHost> StartAsync(Action<PlatformBackendOptions> configure)
        => BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault), configure);

    private static void ListOrigins(PlatformBackendOptions options)
    {
        options.AdditionalHubOrigins.Add("HTTP://LocalHost:8080/");
        options.AdditionalHubOrigins.Add("https://app.example:443");
        options.AdditionalHubOrigins.Add("http://dev.example:80");
    }

    [Fact]
    public void the_list_is_empty_by_default()
        => new PlatformBackendOptions().AdditionalHubOrigins.Should().BeEmpty();

    [Theory]
    [InlineData(BackendHost.OwnOrigin)]
    [InlineData("http://localhost:8080")]
    [InlineData("HTTP://LOCALHOST:8080")]
    [InlineData("https://app.example")]
    [InlineData("http://dev.example")]
    public async Task a_hub_accepts_the_sites_own_origin_and_every_listed_one(string origin)
    {
        //Arrange
        await using var host = await StartAsync(ListOrigins);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, withMarker: true, origin: origin);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("http://localhost:8081")]
    [InlineData("https://localhost:8080")]
    [InlineData("http://localhost")]
    [InlineData("http://app.example")]
    [InlineData("https://app.example:8443")]
    [InlineData("http://localhost:8080/")]
    [InlineData("https://evil.example")]
    [InlineData("null")]
    [InlineData(null)]
    public async Task a_hub_still_refuses_every_origin_that_is_not_listed(string origin)
    {
        //Arrange
        await using var host = await StartAsync(ListOrigins);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, withMarker: true, origin: origin);

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden_origin");
        problem.GetProperty("title").GetString().Should().Be("The connection was refused because it did not come from this site.");
    }

    [Theory]
    [InlineData("localhost:8080")]
    [InlineData("//localhost:8080")]
    [InlineData("http://localhost:8080/app")]
    [InlineData("http://localhost:8080/?a=1")]
    [InlineData("http://localhost:8080#top")]
    [InlineData("http://*.example")]
    [InlineData("ws://localhost:8080")]
    [InlineData("http://user@localhost:8080")]
    [InlineData("")]
    [InlineData(null)]
    public void UsePlatformBackend_refuses_an_entry_that_is_not_an_origin(string entry)
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.AdditionalHubOrigins.Add(entry));

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().StartWith($"PlatformBackendOptions.AdditionalHubOrigins has the entry \"{entry}\", which is not an origin");
    }

    [Fact]
    public async Task with_the_check_off_the_list_is_ignored()
    {
        //Arrange
        await using var host = await StartAsync(options =>
        {
            options.RequireHubOrigin = false;
            options.AdditionalHubOrigins.Add("not an origin");
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("POST", Negotiate, withMarker: true, origin: "https://evil.example");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
