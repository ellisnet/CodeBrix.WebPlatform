using System;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeBrix.WebPlatform.Backend;

/// <summary>The services call of a platform backend.</summary>
public static class PlatformBackendServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform backend's options and its MVC settings: model-binding failures are
    /// answered with 400 <c>validation_failed</c> (<see cref="PlatformBackendOptions.AnswerModelBindingFailures"/>),
    /// and MVC writes no problem details of its own for bodiless 4xx answers, which the pipeline
    /// call answers with the standard error body instead
    /// (<see cref="PlatformBackendOptions.AddProblemBodiesToStatusCodes"/>). The MVC settings are
    /// applied after every other MVC configuration, so the order of this call and
    /// <c>AddControllers()</c> does not matter. Then call
    /// <see cref="PlatformBackendApplicationExtensions.UsePlatformBackend(Microsoft.AspNetCore.Builder.WebApplication)"/>
    /// on the built application.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configure">Sets the options; null keeps every default (every piece on, no
    /// Content-Security-Policy). May be called more than once; the settings add up.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddPlatformBackend(this IServiceCollection services, Action<PlatformBackendOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<PlatformBackendOptions>();
        if (configure != null)
        {
            services.Configure(configure);
        }

        if (services.Any(d => d.ServiceType == typeof(PlatformBackendRegistration)))
        {
            return services;
        }

        services.AddSingleton<PlatformBackendRegistration>();
        services.AddOptions<ApiBehaviorOptions>()
            .PostConfigure<IOptions<PlatformBackendOptions>>((api, platform) =>
            {
                if (platform.Value.AnswerModelBindingFailures)
                {
                    api.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
                }

                if (platform.Value.AddProblemBodiesToStatusCodes)
                {
                    api.SuppressMapClientErrors = true;
                }
            });
        return services;
    }
}
