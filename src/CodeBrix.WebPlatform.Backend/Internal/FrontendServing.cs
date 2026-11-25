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
/// Serves the built frontend's and the web root's files and the frontend applications'
/// single-page-app fallbacks, and answers unknown API addresses.
/// </summary>
internal static class FrontendServing
{
    /// <summary>
    /// The key of the <see cref="HttpContext.Items"/> entry that marks an answer as a served
    /// file, so that it is never compressed as it is sent.
    /// </summary>
    internal static readonly object FrontendFileItem = new();

    private static readonly string[] ReadMethods = { HttpMethods.Get, HttpMethods.Head };

    /// <summary>
    /// The static files of <paramref name="provider"/>; every file they serve is marked as a
    /// served file and gets its Cache-Control (<see cref="CacheControlFor"/>).
    /// </summary>
    public static StaticFileOptions CreateStaticFileOptions(IFileProvider provider, FrontendFileProvider files,
        IContentTypeProvider contentTypes, string hashedCacheControl, string unhashedCacheControl)
    {
        return new StaticFileOptions
        {
            FileProvider = provider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = file =>
            {
                file.Context.Items[FrontendFileItem] = true;
                var cacheControl = CacheControlFor(files.IsBuiltFile(file.File), file.File.Name, hashedCacheControl, unhashedCacheControl);
                if (cacheControl != null)
                {
                    file.Context.Response.Headers.CacheControl = cacheControl;
                }
            },
        };
    }

    /// <summary>
    /// The Cache-Control of a served file: <paramref name="hashed"/> for a built frontend's file
    /// whose name carries dx's content hash, <paramref name="unhashed"/> for every other file
    /// (the web root's whatever their names); null for none.
    /// </summary>
    public static string CacheControlFor(bool builtFile, string name, string hashed, string unhashed)
        => builtFile && HashedFileNames.IsHashed(name) ? hashed : unhashed;

    /// <summary>
    /// Serves the files, preceded, when <paramref name="precompressed"/> is on, by the answer
    /// with a built frontend's file's compressed copy.
    /// </summary>
    public static void UseFiles(IApplicationBuilder app, StaticFileOptions options, FrontendFileProvider files, bool precompressed,
        string hashedCacheControl, string unhashedCacheControl)
    {
        if (precompressed)
        {
            var middleware = new Func<RequestDelegate, RequestDelegate>(next =>
                new PrecompressedFileMiddleware(next, files, options.ContentTypeProvider, hashedCacheControl, unhashedCacheControl).InvokeAsync);
            app.Use(middleware);
        }

        app.UseStaticFiles(options);
    }

    /// <summary>
    /// Maps the fallbacks: for the default application every address that does not look like a
    /// file; for a named application X, <c>/X</c> and below; with no default application, only
    /// <c>/</c>, from the web root's own index.html when there is one. Each answers GET and HEAD
    /// only, never below <c>/api</c>, and never wins over a controller or hub (fallbacks have
    /// the lowest priority). The index.html served is the one the static files would serve at
    /// its address (<see cref="PlatformBackendOptions.FilePrecedence"/>).
    /// </summary>
    public static void MapFallbacks(WebApplication app, FrontendApplicationSet applications, Action<IApplicationBuilder> useFiles)
    {
        var defaultApplication = applications?.Default;
        if (defaultApplication != null)
        {
            MapFallback(app, "{*path:nonfile}", "index.html", useFiles, "default frontend application");
        }
        else
        {
            MapFallback(app, "/", "index.html", useFiles, "site root without a default frontend application");
        }

        foreach (var application in applications?.Named ?? Array.Empty<FrontendApplication>())
        {
            MapFallback(app, application.BasePath + "/{*path:nonfile}", application.Folder + "/index.html", useFiles,
                "frontend application " + application.Name);
        }
    }

    /// <summary>Answers every address below <c>/api</c> that nothing else answers with 404 <c>not_found</c>.</summary>
    public static void MapUnknownApiRoutes(WebApplication app)
    {
        app.MapFallback(PlatformBackendPaths.Api.TrimStart('/') + "/{**path}",
                context => ApiProblemWriter.WriteAsync(context, ApiProblems.NotFound(ApiProblems.NoSuchApiRouteTitle)))
            .AllowAnonymous()
            .WithDisplayName("CodeBrix.WebPlatform.Backend: unknown API route");
    }

    private static void MapFallback(WebApplication app, string pattern, string file, Action<IApplicationBuilder> useFiles, string description)
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
        useFiles(branch);
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

    /// <summary>
    /// Logs, once at start-up, which frontend applications are served, the applications whose
    /// index.html the built frontend lacks, and the addresses both folders have files at.
    /// </summary>
    public static void Describe(FrontendApplicationSet applications, FrontendFileProvider files, FrontendFilePrecedence precedence, ILogger logger)
    {
        if (applications == null)
        {
            logger.LogInformation("No frontend manifest was found in {Folder}/, so no frontend application is served; build the frontend to serve one. The API is served as usual.",
                BuiltFrontend.FolderName);
        }
        else
        {
            logger.LogInformation("Serving the frontend applications {Applications} from {Folder}/.",
                string.Join(", ", applications.Select(a => a.IsDefault ? a.Name + " at /" : a.Name + " at /" + a.BasePath)), BuiltFrontend.FolderName);
            foreach (var application in applications)
            {
                var index = application.IsDefault ? "index.html" : application.Folder + "/index.html";
                if (!files.Built.GetFileInfo(index).Exists)
                {
                    logger.LogWarning("The frontend application {Application} has no {Index} in {Folder}/, so its addresses answer 404; build the frontend.",
                        application.Name, index, BuiltFrontend.FolderName);
                }
            }
        }

        var clashes = files.FindClashes();
        if (clashes.Count > 0)
        {
            const int Listed = 20;
            var listed = string.Join(", ", clashes.Take(Listed)) + (clashes.Count > Listed ? $" and {clashes.Count - Listed} more" : string.Empty);
            if (precedence == FrontendFilePrecedence.WebRootWins)
            {
                logger.LogWarning("{Count} address(es) name a file in both wwwroot/ and {Folder}/; the wwwroot/ file is served and the built frontend's is hidden (FilePrecedence is WebRootWins): {Addresses}.",
                    clashes.Count, BuiltFrontend.FolderName, listed);
            }
            else
            {
                logger.LogWarning("{Count} address(es) name a file in both {Folder}/ and wwwroot/; the built frontend's file is served and the wwwroot/ file is hidden (FilePrecedence is BuiltFrontendWins): {Addresses}.",
                    clashes.Count, BuiltFrontend.FolderName, listed);
            }
        }
    }
}
