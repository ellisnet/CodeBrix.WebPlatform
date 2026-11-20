using System;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Applications;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeBrix.WebPlatform.Backend;

/// <summary>The pipeline call of a platform backend.</summary>
public static class PlatformBackendApplicationExtensions
{
    /// <summary>The logging category of the library's start-up messages.</summary>
    public const string LogCategory = "CodeBrix.WebPlatform.Backend";

    /// <summary>
    /// Adds the platform's part of the pipeline and serves the frontend applications that the
    /// frontend manifest in the web root names (read once, here). Without a manifest no frontend
    /// application is served, and the API works as usual. Call it early: after
    /// <c>builder.Build()</c> and anything that must see every answer (<c>UseHsts</c>, for
    /// example), and BEFORE the application's own <c>UseRouting</c>, rate limiter,
    /// authentication, authorization, <c>MapControllers</c> and hubs.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddPlatformBackend</c> was not called, the
    /// request marker check is on and the backend declares no marker, or the
    /// Content-Security-Policy is not one line; the message says what to do.</exception>
    /// <exception cref="FrontendManifestException">The web root has a manifest that cannot be used.</exception>
    public static WebApplication UsePlatformBackend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Use(app, null, readManifest: true);
    }

    /// <summary>
    /// The same as <see cref="UsePlatformBackend(WebApplication)"/>, but serves the frontend
    /// applications given instead of reading the manifest (for tests, and for a developer who
    /// prefers to say it in code). Each is served from its folder in the web root.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="applications">The frontend applications to serve.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> or <paramref name="applications"/> is null.</exception>
    /// <exception cref="InvalidOperationException">As for <see cref="UsePlatformBackend(WebApplication)"/>,
    /// and when <see cref="PlatformBackendOptions.ServeFrontendApplications"/> is switched off.</exception>
    public static WebApplication UsePlatformBackend(this WebApplication app, FrontendApplicationSet applications)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(applications);
        return Use(app, applications, readManifest: false);
    }

    private static WebApplication Use(WebApplication app, FrontendApplicationSet applications, bool readManifest)
    {
        if (app.Services.GetService<PlatformBackendRegistration>() == null)
        {
            throw new InvalidOperationException(
                "UsePlatformBackend needs the platform backend's services: call builder.Services.AddPlatformBackend(...) before builder.Build().");
        }

        var options = app.Services.GetRequiredService<IOptions<PlatformBackendOptions>>().Value;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory);
        var environment = app.Environment;

        //Everything that can be wrong is found before the pipeline is touched.
        var contentSecurityPolicy = options.SendSecurityHeaders ? CheckContentSecurityPolicy(options.ContentSecurityPolicy) : null;
        var marker = options.RequireRequestMarker ? RequestMarkerResolver.Resolve(options, environment.ApplicationName) : null;
        if (!Enum.IsDefined(options.CompressResponses))
        {
            throw new InvalidOperationException(
                $"PlatformBackendOptions.CompressResponses is {(int)options.CompressResponses}, which is not a ResponseCompression value. Set it to None (the default), FileAnswers or FileAnswersAndJson.");
        }
        if (!options.ServeFrontendApplications && applications != null)
        {
            throw new InvalidOperationException(
                "UsePlatformBackend was given frontend applications to serve, but PlatformBackendOptions.ServeFrontendApplications is switched off. "
                + "Switch it on, or call UsePlatformBackend() without applications.");
        }

        if (options.ServeFrontendApplications && readManifest && !string.IsNullOrEmpty(environment.WebRootPath))
        {
            applications = FrontendManifestReader.Read(environment.WebRootPath);
        }

        if (options.CompressResponses != ResponseCompression.None)
        {
            //First, so that every answer below it - the platform's own problem bodies included - can be compressed.
            var provider = new PlatformResponseCompressionProvider(app.Services, options.CompressResponses);
            app.Use(next => new ResponseCompressionMiddleware(next, provider).Invoke);
        }

        if (options.SendSecurityHeaders)
        {
            app.Use(next => new SecurityHeadersMiddleware(next, contentSecurityPolicy).InvokeAsync);
        }

        if (options.AddProblemBodiesToStatusCodes)
        {
            app.UseStatusCodePages(ApiStatusCodePages.HandleAsync);
        }

        if (options.RequireRequestMarker)
        {
            app.Use(next => new RequestMarkerMiddleware(next, marker).InvokeAsync);
        }

        if (options.RequireHubOrigin)
        {
            app.Use(next => new HubOriginMiddleware(next).InvokeAsync);
        }

        if (options.ServeFrontendApplications)
        {
            var files = FrontendServing.CreateStaticFileOptions(environment.WebRootFileProvider, environment.WebRootPath);
            FrontendServing.UseFiles(app, files, options.ServePrecompressedFiles);
            FrontendServing.MapFallbacks(app, applications, files, options.ServePrecompressedFiles, logger);
        }

        if (options.AnswerUnknownApiRoutes)
        {
            FrontendServing.MapUnknownApiRoutes(app);
        }

        return app;
    }

    private static string CheckContentSecurityPolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            return null;
        }

        if (policy.Any(c => c < ' ' || c == '\u007f'))
        {
            throw new InvalidOperationException(
                "PlatformBackendOptions.ContentSecurityPolicy must be one line of text, without line breaks or other control characters; separate its directives with semicolons.");
        }

        return policy.Trim();
    }
}
