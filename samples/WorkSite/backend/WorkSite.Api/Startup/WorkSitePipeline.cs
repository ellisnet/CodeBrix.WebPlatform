using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting;
using WorkSite.Api.Hubs;
using WorkSite.Api.Infrastructure;

namespace WorkSite.Api.Startup;

/// <summary>Builds the request pipeline.</summary>
public static class WorkSitePipeline
{
    /// <summary>
    /// Security headers, HSTS outside Development, contract error bodies, cross-site request
    /// protection for the API and for the hubs, static files with an <c>index.html</c> fallback
    /// for app routes, rate limiting, authentication, the controllers and the hubs.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseWorkSite(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseStatusCodePages(ApiStatusCodePages.HandleAsync);
        app.UseMiddleware<RequestedWithMiddleware>();
        app.UseMiddleware<HubOriginMiddleware>();

        var staticFiles = new StaticFileOptions { ContentTypeProvider = ContentTypes() };
        app.UseDefaultFiles();
        app.UseStaticFiles(staticFiles);

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapWorkSiteHubs();
        //Extension-less paths outside /api are app routes handled by the Dioxus router.
        //(/api paths never reach here: ApiNotFoundController answers them.)
        app.MapFallbackToFile("index.html", staticFiles);
        return app;
    }

    private static FileExtensionContentTypeProvider ContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".wasm"] = "application/wasm";
        return provider;
    }
}
