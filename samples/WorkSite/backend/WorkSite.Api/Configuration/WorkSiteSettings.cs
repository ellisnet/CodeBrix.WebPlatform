using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace WorkSite.Api.Configuration;

/// <summary>
/// Every WorkSite setting, read from configuration (environment variables use a double
/// underscore as the section separator) and checked once at startup.
/// </summary>
public sealed class WorkSiteSettings
{
    /// <summary>The configuration key of the database connection string.</summary>
    public const string ConnectionStringName = "WorkSite";

    /// <summary>The PostgreSQL connection string (<c>ConnectionStrings__WorkSite</c>, required).</summary>
    public string ConnectionString { get; set; }

    /// <summary>Access token signing.</summary>
    public JwtSettings Jwt { get; set; } = new();

    /// <summary>Sign-in lifetimes and password rules.</summary>
    public AuthSettings Auth { get; set; } = new();

    /// <summary>The optional demo account.</summary>
    public DemoSettings Demo { get; set; } = new();

    /// <summary>Response security settings.</summary>
    public SecuritySettings Security { get; set; } = new();

    /// <summary>Limits on the sign-in, register and refresh routes.</summary>
    public RateLimitSettings RateLimiting { get; set; } = new();

    /// <summary>Reads and checks the settings.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="WorkSiteConfigurationException">A required value is missing or a value is invalid.</exception>
    public static WorkSiteSettings Load(IConfiguration configuration)
    {
        var settings = new WorkSiteSettings
        {
            ConnectionString = configuration.GetConnectionString(ConnectionStringName),
        };
        configuration.GetSection("Jwt").Bind(settings.Jwt);
        configuration.GetSection("Auth").Bind(settings.Auth);
        configuration.GetSection("Demo").Bind(settings.Demo);
        configuration.GetSection("Security").Bind(settings.Security);
        configuration.GetSection("RateLimiting").Bind(settings.RateLimiting);
        if (string.IsNullOrWhiteSpace(settings.Security.ContentSecurityPolicy))
        {
            settings.Security.ContentSecurityPolicy = SecuritySettings.DefaultContentSecurityPolicy;
        }

        var problems = settings.Validate();
        if (problems.Count > 0)
        {
            throw new WorkSiteConfigurationException(problems);
        }

        return settings;
    }

    /// <summary>Lists what is wrong with these settings.</summary>
    /// <returns>One sentence per problem; empty when the settings are usable.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            problems.Add("ConnectionStrings__WorkSite is required: the PostgreSQL connection string, " +
                         "for example Host=db;Database=worksite;Username=worksite;Password=...");
        }

        if (string.IsNullOrWhiteSpace(Jwt.SigningKey))
        {
            problems.Add("Jwt__SigningKey is required: a random secret of at least 32 bytes used to sign access tokens.");
        }
        else if (Encoding.UTF8.GetByteCount(Jwt.SigningKey) < 32)
        {
            problems.Add("Jwt__SigningKey must be at least 32 bytes long.");
        }

        if (string.IsNullOrWhiteSpace(Jwt.Issuer))
        {
            problems.Add("Jwt__Issuer must not be empty.");
        }

        if (Auth.AccessTokenMinutes < 1)
        {
            problems.Add("Auth__AccessTokenMinutes must be at least 1.");
        }

        if (Auth.RefreshTokenDays < 1)
        {
            problems.Add("Auth__RefreshTokenDays must be at least 1.");
        }

        if (Auth.MinimumPasswordLength < 1 || Auth.MinimumPasswordLength > Auth.MaximumPasswordLength)
        {
            problems.Add($"Auth__MinimumPasswordLength must be between 1 and {Auth.MaximumPasswordLength}.");
        }

        if (Demo.Enabled)
        {
            if (string.IsNullOrWhiteSpace(Demo.Username))
            {
                problems.Add("Demo__Username must not be empty when Demo__Enabled is true.");
            }

            if (string.IsNullOrEmpty(Demo.Password))
            {
                problems.Add("Demo__Password is required when Demo__Enabled is true.");
            }
        }

        if (RateLimiting.AuthPermitLimit < 1)
        {
            problems.Add("RateLimiting__AuthPermitLimit must be at least 1.");
        }

        if (RateLimiting.AuthWindowSeconds < 1)
        {
            problems.Add("RateLimiting__AuthWindowSeconds must be at least 1.");
        }

        return problems;
    }
}
