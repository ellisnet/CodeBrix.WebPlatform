using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Applications;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
    /// frontend manifest in the built frontend (<c>frontend-built/</c> in the content root,
    /// <see cref="BuiltFrontend.FolderName"/>) names (read once, here), and the developer's own
    /// files of the web root (<c>wwwroot/</c>). Without a manifest no frontend application is
    /// served, and the API works as usual. While the offline file is in the web root the backend
    /// is offline (<see cref="PlatformBackendPaths.OfflineFileName"/>). Call it early: after
    /// <c>builder.Build()</c> and anything that must see every answer (<c>UseHsts</c>, for
    /// example), and BEFORE the application's own <c>UseRouting</c>, rate limiter,
    /// authentication, authorization, <c>MapControllers</c> and hubs.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddPlatformBackend</c> was not called, the
    /// request marker check is on and the backend declares no marker, or an option has a value
    /// that cannot be used (a Content-Security-Policy or Cache-Control that is not one line, an
    /// entry of a table or set that cannot be read); the message says what to do.</exception>
    /// <exception cref="FrontendManifestException">The built frontend has a manifest that cannot be used.</exception>
    public static WebApplication UsePlatformBackend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Use(app, null, readManifest: true);
    }

    /// <summary>
    /// The same as <see cref="UsePlatformBackend(WebApplication)"/>, but serves the frontend
    /// applications given instead of reading the manifest (for tests, and for a developer who
    /// prefers to say it in code). Each is served from its folder in the built frontend.
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
        var contentSecurityPolicy = options.SendSecurityHeaders ? CheckOneLine(options.ContentSecurityPolicy, nameof(PlatformBackendOptions.ContentSecurityPolicy), "separate its directives with semicolons") : null;
        var hashedCacheControl = CheckOneLine(options.HashedFileCacheControl, nameof(PlatformBackendOptions.HashedFileCacheControl), "separate its directives with commas");
        var unhashedCacheControl = CheckOneLine(options.UnhashedFileCacheControl, nameof(PlatformBackendOptions.UnhashedFileCacheControl), "separate its directives with commas");
        var marker = options.RequireRequestMarker ? RequestMarkerResolver.Resolve(options, environment.ApplicationName) : null;
        if (!Enum.IsDefined(options.CompressResponses))
        {
            throw new InvalidOperationException(
                $"PlatformBackendOptions.CompressResponses is {(int)options.CompressResponses}, which is not a ResponseCompression value. Set it to None (the default), FileAnswers or FileAnswersAndJson.");
        }
        if (!Enum.IsDefined(options.FilePrecedence))
        {
            throw new InvalidOperationException(
                $"PlatformBackendOptions.FilePrecedence is {(int)options.FilePrecedence}, which is not a FrontendFilePrecedence value. Set it to BuiltFrontendWins (the default) or WebRootWins.");
        }
        var additionalHubOrigins = options.RequireHubOrigin ? HubOriginMiddleware.ParseOrigins(options.AdditionalHubOrigins) : null;
        var compressedFileTypes = MediaTypePatterns.Parse(options.CompressedFileTypes, nameof(PlatformBackendOptions.CompressedFileTypes));
        var contentTypes = ContentTypeTable.Create(options.StaticFileContentTypes);
        if (!options.ServeFrontendApplications && applications != null)
        {
            throw new InvalidOperationException(
                "UsePlatformBackend was given frontend applications to serve, but PlatformBackendOptions.ServeFrontendApplications is switched off. "
                + "Switch it on, or call UsePlatformBackend() without applications.");
        }

        var builtFrontendPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, BuiltFrontend.FolderName));
        if (options.ServeFrontendApplications && readManifest)
        {
            applications = FrontendManifestReader.Read(builtFrontendPath);
        }

        IFileProvider builtFiles = new NullFileProvider();
        if (Directory.Exists(builtFrontendPath))
        {
            var physical = new PhysicalFileProvider(builtFrontendPath);
            app.Lifetime.ApplicationStopped.Register(physical.Dispose);
            builtFiles = physical;
        }

        //A web root that did not exist at start-up is read from its folder as soon as the folder exists.
        var webRootFiles = environment.WebRootFileProvider;
        if (webRootFiles is null or NullFileProvider)
        {
            var webRootPath = string.IsNullOrEmpty(environment.WebRootPath) ? Path.Combine(environment.ContentRootPath, "wwwroot") : environment.WebRootPath;
            var late = new LateWebRootFileProvider(webRootPath);
            app.Lifetime.ApplicationStopped.Register(late.Dispose);
            webRootFiles = late;
        }

        var files = new FrontendFileProvider(builtFiles, builtFrontendPath, webRootFiles, options.FilePrecedence);
        var offlineFile = new OfflineFile(webRootFiles);

        if (options.CompressResponses != ResponseCompression.None)
        {
            //First, so that every answer below it - the platform's own problem bodies included - can be compressed.
            var provider = new PlatformResponseCompressionProvider(app.Services, options.CompressResponses, compressedFileTypes);
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

        //Before the request marker and the hub Origin checks: offline, every API request gets the same 503.
        var offlineBranch = ((IApplicationBuilder)app).New();
        offlineBranch.UseStaticFiles(FrontendServing.CreateStaticFileOptions(files.Own, files, contentTypes, hashedCacheControl, unhashedCacheControl));
        offlineBranch.Run(context => OfflineMiddleware.WritePageAsync(context, offlineFile));
        var offline = offlineBranch.Build();
        app.Use(next => new OfflineMiddleware(next, offlineFile, offline).InvokeAsync);
        if (offlineFile.IsPresent())
        {
            logger.LogWarning("The offline file {File} is in the web root, so the backend answers 503 until it is removed.", PlatformBackendPaths.OfflineFileName);
        }

        if (options.RequireRequestMarker)
        {
            app.Use(next => new RequestMarkerMiddleware(next, marker).InvokeAsync);
        }

        if (options.RequireHubOrigin)
        {
            app.Use(next => new HubOriginMiddleware(next, additionalHubOrigins).InvokeAsync);
        }

        if (options.ServeFrontendApplications)
        {
            var staticFiles = FrontendServing.CreateStaticFileOptions(files, files, contentTypes, hashedCacheControl, unhashedCacheControl);
            void UseFiles(IApplicationBuilder builder)
                => FrontendServing.UseFiles(builder, staticFiles, files, options.ServePrecompressedFiles, hashedCacheControl, unhashedCacheControl);
            UseFiles(app);
            FrontendServing.MapFallbacks(app, applications, UseFiles);
            FrontendServing.Describe(applications, files, options.FilePrecedence, logger);
        }

        if (options.AnswerUnknownApiRoutes)
        {
            FrontendServing.MapUnknownApiRoutes(app);
        }

        return app;
    }

    private static string CheckOneLine(string value, string optionName, string separatorAdvice)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Any(c => c < ' ' || c == '\u007f'))
        {
            throw new InvalidOperationException(
                $"PlatformBackendOptions.{optionName} must be one line of text, without line breaks or other control characters; {separatorAdvice}.");
        }

        return value.Trim();
    }
}
