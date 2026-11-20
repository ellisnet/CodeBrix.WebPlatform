using System;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests;

public class PlatformBackendServiceCollectionExtensionsTests
{
    private static ServiceProvider Provider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPlatformBackend_refuses_null_services()
    {
        //Act
        Action act = () => PlatformBackendServiceCollectionExtensions.AddPlatformBackend(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void every_piece_is_on_by_default_and_there_is_no_policy()
    {
        //Arrange
        using var provider = Provider(services => services.AddPlatformBackend());

        //Act
        var options = provider.GetRequiredService<IOptions<PlatformBackendOptions>>().Value;

        //Assert
        options.ServeFrontendApplications.Should().BeTrue();
        options.AnswerUnknownApiRoutes.Should().BeTrue();
        options.AddProblemBodiesToStatusCodes.Should().BeTrue();
        options.AnswerModelBindingFailures.Should().BeTrue();
        options.RequireRequestMarker.Should().BeTrue();
        options.RequireHubOrigin.Should().BeTrue();
        options.SendSecurityHeaders.Should().BeTrue();
        options.ContentSecurityPolicy.Should().BeNull();
        options.RequestMarker.Should().BeNull();
        options.BackendAssembly.Should().BeNull();
    }

    [Fact]
    public void the_mvc_settings_hold_whether_AddControllers_comes_before_or_after()
    {
        //Arrange
        using var after = Provider(services =>
        {
            services.AddPlatformBackend();
            services.AddControllers();
        });
        using var before = Provider(services =>
        {
            services.AddControllers();
            services.AddPlatformBackend();
        });

        //Act
        var optionsAfter = after.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var optionsBefore = before.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        //Assert
        foreach (var options in new[] { optionsAfter, optionsBefore })
        {
            options.InvalidModelStateResponseFactory.Should().Be((Func<ActionContext, IActionResult>)InvalidModelStateResponse.Create);
            options.SuppressMapClientErrors.Should().BeTrue();
        }
    }

    [Fact]
    public void switched_off_pieces_leave_the_mvc_settings_alone()
    {
        //Arrange
        using var provider = Provider(services =>
        {
            services.AddControllers();
            services.AddPlatformBackend(options =>
            {
                options.AnswerModelBindingFailures = false;
                options.AddProblemBodiesToStatusCodes = false;
            });
        });

        //Act
        var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        //Assert
        options.InvalidModelStateResponseFactory.Should().NotBe((Func<ActionContext, IActionResult>)InvalidModelStateResponse.Create);
        options.SuppressMapClientErrors.Should().BeFalse();
    }

    [Fact]
    public void calling_it_twice_adds_up_the_settings_and_registers_once()
    {
        //Arrange
        var services = new ServiceCollection();

        //Act
        services.AddPlatformBackend(options => options.ContentSecurityPolicy = "default-src 'self'");
        services.AddPlatformBackend(options => options.RequireHubOrigin = false);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PlatformBackendOptions>>().Value;

        //Assert
        options.ContentSecurityPolicy.Should().Be("default-src 'self'");
        options.RequireHubOrigin.Should().BeFalse();
        services.Count(d => d.ServiceType == typeof(PlatformBackendRegistration)).Should().Be(1);
    }
}
