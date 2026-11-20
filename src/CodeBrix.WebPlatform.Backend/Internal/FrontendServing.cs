using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Serves the web root's files and the frontend applications' single-page-app fallbacks, and
/// answers unknown API addresses.
/// </summary>
internal static class FrontendServing
{
    /// <summary>
    /// The key of the <see cref="HttpContext.Items"/> entry that marks an answer as a web-root
    /// file, so that it is never compressed as it is sent.
    /// </summary>
    internal static readonly object FrontendFileItem = new();

    private static readonly string[] ReadMethods = { HttpMethods.Get, HttpMethods.Head };

    /// <summary>
    /// The static files, read from the web root without the manifest and without the
    /// compressed copies; every file they serve is marked as a web-root file.
    /// </summary>
    public static StaticFileOptions CreateStaticFileOptions(IFileProvider webRootFiles, string webRootPath)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        return new StaticFileOptions
        {
            FileProvider = new ManifestHidingFileProvider(webRootFiles ?? new NullFileProvider(), webRootPath),
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = file => file.Context.Items[FrontendFileItem] = true,
        };
    }

    /// <summary>
    /// Serves the web root's files, preceded, when <paramref name="precompressed"/> is on, by the
    /// answer with a file's compressed copy.
    /// </summary>
    public static void UseFiles(IApplicationBuilder app, StaticFileOptions files, bool precompressed)
    {
        if (precompressed)
        {
            var middleware = new Func<RequestDelegate, RequestDelegate>(next =>
                new PrecompressedFileMiddleware(next, (ManifestHidingFileProvider)files.FileProvider, files.ContentTypeProvider).InvokeAsync);
            app.Use(middleware);
        }

        app.UseStaticFiles(files);
    }

    /// <summary>
    /// Maps the fallbacks: for the default application every address that does not look like a
    /// file; for a named application X, <c>/X</c> and below; with no default application, only
    /// <c>/</c>, from the web root's own index.html when there is one. Each answers GET and HEAD
    /// only, never below <c>/api</c>, and never wins over a controller or hub (fallbacks have
    /// the lowest priority).
    /// </summary>
    public static void MapFallbacks(WebApplication app, FrontendApplicationSet applications, StaticFileOptions files, bool precompressed, ILogger logger)
    {
        var defaultApplication = applications?.Default;
        if (defaultApplication != null)
        {
            MapFallback(app, "{*path:nonfile}", "index.html", files, precompressed, "default frontend application");
        }
        else
        {
            MapFallback(app, "/", "index.html", files, precompressed, "site root without a default frontend application");
        }

        foreach (var application in applications?.Named ?? Array.Empty<FrontendApplication>())
        {
            MapFallback(app, application.BasePath + "/{*path:nonfile}", application.Folder + "/index.html", files, precompressed,
                "frontend application " + application.Name);
        }

        Describe(applications, files.FileProvider, logger);
    }

    /// <summary>Answers every address below <c>/api</c> that nothing else answers with 404 <c>not_found</c>.</summary>
    public static void MapUnknownApiRoutes(WebApplication app)
    {
        app.MapFallback(PlatformBackendPaths.Api.TrimStart('/') + "/{**path}",
                context => ApiProblemWriter.WriteAsync(context, ApiProblems.NotFound(ApiProblems.NoSuchApiRouteTitle)))
            .AllowAnonymous()
            .WithDisplayName("CodeBrix.WebPlatform.Backend: unknown API route");
    }

    private static void MapFallback(WebApplication app, string pattern, string file, StaticFileOptions files, bool precompressed, string description)
    {
        var branch = ((IEndpointRouteBuilder)app).CreateApplicationBuilder();
        var path = new PathString("/" + file);
        branch.Use(next => context =>
        {
            context.Request.Path = path;
            //Static files hand a request with an endpoint on to the endpoint; this one is served as a file.
            context.SetEndpoint(null);
            return next(context);
        });
        UseFiles(branch, files, precompressed);
        var serve = branch.Build();

        app.MapFallback(pattern, context =>
            {
                if (ApiPaths.IsApi(context.Request.Path))
                {
                    //Nothing below /api is ever answered with an index.html.
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }

                return serve(context);
            })
            .WithMetadata(new HttpMethodMetadata(ReadMethods))
            .AllowAnonymous()
            .WithDisplayName("CodeBrix.WebPlatform.Backend: " + description);
    }

    private static void Describe(FrontendApplicationSet applications, IFileProvider files, ILogger logger)
    {
        if (applications == null)
        {
            logger.LogInformation("No frontend manifest was found in the web root, so no frontend application is served; build the frontend to serve one. The API is served as usual.");
            return;
        }

        logger.LogInformation("Serving the frontend applications {Applications}.",
            string.Join(", ", applications.Select(a => a.IsDefault ? a.Name + " at /" : a.Name + " at /" + a.BasePath)));
        foreach (var application in applications)
        {
            var index = application.IsDefault ? "index.html" : application.Folder + "/index.html";
            if (!files.GetFileInfo(index).Exists)
            {
                logger.LogWarning("The frontend application {Application} has no {Index} in the web root, so its addresses answer 404; build the frontend into the web root.",
                    application.Name, index);
            }
        }
    }
}
