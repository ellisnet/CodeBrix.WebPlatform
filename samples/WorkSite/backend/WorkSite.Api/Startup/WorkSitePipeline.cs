using CodeBrix.WebPlatform.Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using WorkSite.Api.Hubs;

namespace WorkSite.Api.Startup;

/// <summary>Builds the request pipeline.</summary>
public static class WorkSitePipeline
{
    /// <summary>
    /// HSTS outside Development, the platform backend (security headers, contract error bodies,
    /// cross-site request protection for the API and for the hubs, the web root's files and the
    /// frontend served as the frontend manifest says), rate limiting, authentication, the
    /// controllers and the hubs.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseWorkSite(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UsePlatformBackend();

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapWorkSiteHubs();
        return app;
    }
}
