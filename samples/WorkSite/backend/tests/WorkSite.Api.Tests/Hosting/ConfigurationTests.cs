using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Hosting;

public class ConfigurationTests
{
    [Fact]
    public async Task startup_fails_clearly_without_a_connection_string_or_signing_key()
    {
        //Act
        var message = await StartupFailureAsync(new Dictionary<string, string>());

        //Assert
        message.Should().Contain("ConnectionStrings__WorkSite is required");
        message.Should().Contain("Jwt__SigningKey is required");
    }

    [Fact]
    public async Task startup_fails_clearly_with_a_short_signing_key()
    {
        //Arrange
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:WorkSite"] = "Host=127.0.0.1;Database=never_used",
            ["Jwt:SigningKey"] = "too-short",
        };

        //Act
        var message = await StartupFailureAsync(settings);

        //Assert
        message.Should().Contain("Jwt__SigningKey must be at least 32 bytes long.");
        message.Should().NotContain("ConnectionStrings__WorkSite");
    }

    [Fact]
    public async Task startup_fails_clearly_when_the_demo_account_has_no_password()
    {
        //Arrange
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:WorkSite"] = "Host=127.0.0.1;Database=never_used",
            ["Jwt:SigningKey"] = new string('k', 40),
            ["Demo:Enabled"] = "true",
        };

        //Act
        var message = await StartupFailureAsync(settings);

        //Assert
        message.Should().Contain("Demo__Password is required when Demo__Enabled is true.");
    }

    /// <summary>Starts the application with exactly these settings and returns the failure text.</summary>
    private static async Task<string> StartupFailureAsync(IReadOnlyDictionary<string, string> settings)
    {
        await using var factory = new MisconfiguredApiFactory(settings);
        await factory.InitializeAsync();
        Action start = () => factory.CreateClient();

        var failure = start.Should().Throw<Exception>().Which;
        return failure.ToString();
    }
}
