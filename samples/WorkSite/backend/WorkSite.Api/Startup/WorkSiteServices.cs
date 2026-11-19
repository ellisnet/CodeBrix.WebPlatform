using CodeBrix.PostgresClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkSite.Api.Auth;
using WorkSite.Api.Configuration;
using WorkSite.Api.Hubs;
using WorkSite.Api.Infrastructure;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Schema;

namespace WorkSite.Api.Startup;

/// <summary>Registers the WorkSite services.</summary>
public static class WorkSiteServices
{
    /// <summary>Adds settings, data access, authentication, rate limiting, the controllers and the hubs.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddWorkSite(this IServiceCollection services)
    {
        //Read lazily, after every configuration source (including test hosts) has been added.
        services.AddSingleton(provider => WorkSiteSettings.Load(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<WorkSiteSettings>();
            var builder = new PgSqlDataSourceBuilder(settings.ConnectionString);
            builder.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            return builder.Build();
        });

        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<IClientRepository, ClientRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<ITimeEntryRepository, TimeEntryRepository>();
        services.AddSingleton<IDashboardRepository, DashboardRepository>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        services.AddSingleton<SchemaVersionReader>();
        services.AddSingleton<DemoAccountState>();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<CredentialChecker>();
        services.AddSingleton<SessionService>();

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
                options.SuppressMapClientErrors = true;
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerConfigurator>();
        services.AddAuthorization();
        services.AddRateLimiter(RateLimitPolicies.Configure);
        services.AddWorkSiteHubs();
        return services;
    }
}
