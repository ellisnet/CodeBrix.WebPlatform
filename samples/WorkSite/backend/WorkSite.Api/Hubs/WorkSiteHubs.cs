using System;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WorkSite.Api.Hubs;

/// <summary>Registers and maps the SignalR hubs.</summary>
public static class WorkSiteHubs
{
    /// <summary>Adds SignalR, the hubs' shared state and the background tasks that feed them.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddWorkSiteHubs(this IServiceCollection services)
    {
        services.AddSignalR();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IUserIdProvider, HubUserIdProvider>();
        services.AddSingleton<ActivityFeed>();
        services.AddSingleton<RunningTimers>();
        services.AddSingleton<ChangeNotifier>();
        services.AddSingleton<ProjectFeedGroups>();
        services.AddSingleton<SessionConnections>();
        services.AddSingleton<SessionNotifier>();
        services.AddHostedService<ClockTicker>();
        services.AddHostedService<ActivityTicker>();
        services.AddHostedService<TimerTicker>();
        services.AddHostedService<NotificationTicker>();
        services.AddHostedService<ProjectFeedTicker>();
        return services;
    }

    /// <summary>
    /// Maps every hub at the path its <c>[WireHub]</c> attribute declares, which is the path the
    /// generated frontend code connects to.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapWorkSiteHubs(this WebApplication app)
    {
        app.MapHub<ClockHub>(WireHubAttribute.GetPath(typeof(ClockHub)));
        app.MapHub<ActivityHub>(WireHubAttribute.GetPath(typeof(ActivityHub)));
        app.MapHub<EstimatorHub>(WireHubAttribute.GetPath(typeof(EstimatorHub)));
        app.MapHub<TimerHub>(WireHubAttribute.GetPath(typeof(TimerHub)));
        app.MapHub<ChangesHub>(WireHubAttribute.GetPath(typeof(ChangesHub)));
        app.MapHub<NotificationsHub>(WireHubAttribute.GetPath(typeof(NotificationsHub)));
        app.MapHub<ReportProgressHub>(WireHubAttribute.GetPath(typeof(ReportProgressHub)));
        app.MapHub<ProjectFeedHub>(WireHubAttribute.GetPath(typeof(ProjectFeedHub)));
        app.MapHub<SessionHub>(WireHubAttribute.GetPath(typeof(SessionHub)));
        return app;
    }
}
