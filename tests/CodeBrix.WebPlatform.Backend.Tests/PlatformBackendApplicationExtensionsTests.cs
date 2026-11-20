using System;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Builder;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests;

/// <summary>What the pipeline call checks at start-up, and where it finds the request marker.</summary>
public class PlatformBackendApplicationExtensionsTests
{
    private static readonly string AssemblyWithoutMarker = typeof(PlatformBackendApplicationExtensionsTests).Assembly.GetName().Name;

    [Fact]
    public void UsePlatformBackend_refuses_a_null_application()
    {
        //Act
        Action act = () => PlatformBackendApplicationExtensions.UsePlatformBackend(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UsePlatformBackend_without_AddPlatformBackend_says_which_call_is_missing()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, addPlatformBackend: false);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*call builder.Services.AddPlatformBackend(...) before builder.Build()*");
    }

    [Fact]
    public void a_backend_that_declares_no_marker_fails_at_start_up_with_what_to_do()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, applicationName: AssemblyWithoutMarker);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*the backend assembly {AssemblyWithoutMarker} declares none*[assembly: WireRequestMarker(\"YourApp\")]*BackendAssembly*RequireRequestMarker = false*");
    }

    [Fact]
    public void an_application_name_that_is_no_assembly_fails_with_what_to_do()
    {
        //Act
        Action act = () => RequestMarkerResolver.Resolve(new PlatformBackendOptions(), "No.Such.Assembly");

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*No.Such.Assembly, but it cannot be loaded*BackendAssembly*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void a_host_without_an_application_name_fails_with_what_to_do(string applicationName)
    {
        //Act
        Action act = () => RequestMarkerResolver.Resolve(new PlatformBackendOptions(), applicationName);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*the host has no application name*BackendAssembly*");
    }

    [Fact]
    public void the_marker_is_read_from_the_assembly_the_application_name_names()
        => RequestMarkerResolver.Resolve(new PlatformBackendOptions(), BackendHost.SampleApplicationName).Should().Be(SampleApplication.RequestMarker);

    [Fact]
    public async Task the_marker_is_read_from_BackendAssembly_when_it_is_given()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(
            configure: options => options.BackendAssembly = typeof(SampleApplication).Assembly,
            applicationName: AssemblyWithoutMarker);
        using var client = host.CreateClient();

        //Act
        var refused = await client.SendAsync("POST", "/api/account/touch");
        var accepted = await client.SendAsync("POST", "/api/echo", withMarker: true, json: "{\"text\":\"a\",\"count\":1}");

        //Assert
        await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void BackendAssembly_without_a_marker_fails_at_start_up()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.BackendAssembly = typeof(PlatformBackendApplicationExtensionsTests).Assembly);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{AssemblyWithoutMarker} declares none*");
    }

    [Fact]
    public async Task RequestMarker_in_the_options_wins_over_the_assembly()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(configure: options => options.RequestMarker = "Other Value");
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(WireRequestMarkerAttribute.HeaderName, "Other Value");

        //Act
        var response = await client.SendAsync("POST", "/api/echo", json: "{\"text\":\"a\",\"count\":1}");
        var declared = await client.SendAsync("PUT", "/api/echo/conflict", withMarker: true);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await declared.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "missing_request_header");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" padded")]
    [InlineData("two  spaces")]
    [InlineData("line\nbreak")]
    public void a_RequestMarker_that_cannot_be_sent_fails_at_start_up(string marker)
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.RequestMarker = marker);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*PlatformBackendOptions.RequestMarker*cannot be sent in X-Requested-With*");
    }

    [Theory]
    [InlineData("default-src 'self';\r\nscript-src 'self'")]
    [InlineData("default-src 'self';\tscript-src 'self'")]
    public void a_Content_Security_Policy_that_is_not_one_line_fails_at_start_up(string policy)
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.ContentSecurityPolicy = policy);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must be one line*");
    }

    [Fact]
    public void a_Content_Security_Policy_is_not_checked_when_the_headers_are_off()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options =>
        {
            options.SendSecurityHeaders = false;
            options.ContentSecurityPolicy = "a\nb";
        });

        //Act
        Func<WebApplication> act = () => app.UsePlatformBackend();

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void UsePlatformBackend_returns_the_same_application()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root);

        //Act
        var returned = app.UsePlatformBackend();

        //Assert
        returned.Should().BeSameAs(app);
    }
}
