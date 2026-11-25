using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Takes the backend offline while the offline file (<see cref="PlatformBackendPaths.OfflineFileName"/>)
/// is at the web root's top, looking for it on every request: an API or hub request is answered
/// 503 with <see cref="ApiProblems.Offline"/>; any other request goes to the offline branch,
/// which serves the developer's own files of the web root and answers everything else 503 with
/// the offline file's content (<see cref="WritePageAsync"/>). Without the file every request
/// goes on unchanged.
/// </summary>
internal sealed class OfflineMiddleware
{
    /// <summary>The content type of the offline page.</summary>
    public const string PageContentType = "text/html; charset=utf-8";

    /// <summary>The Cache-Control of every offline answer.</summary>
    public const string NoStore = "no-store";

    private readonly RequestDelegate _next;
    private readonly OfflineFile _file;
    private readonly RequestDelegate _offlineBranch;

    /// <summary>The middleware.</summary>
    /// <param name="next">The rest of the pipeline, for a request while online.</param>
    /// <param name="file">The offline file.</param>
    /// <param name="offlineBranch">The offline branch: the web root's own files, then <see cref="WritePageAsync"/>.</param>
    public OfflineMiddleware(RequestDelegate next, OfflineFile file, RequestDelegate offlineBranch)
    {
        _next = next;
        _file = file;
        _offlineBranch = offlineBranch;
    }

    /// <summary>Answers a request while offline; hands it on while online.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        if (!_file.IsPresent())
        {
            return _next(context);
        }

        if (ApiPaths.IsApi(context.Request.Path))
        {
            context.Response.Headers.CacheControl = NoStore;
            return ApiProblemWriter.WriteAsync(context, ApiProblems.Offline());
        }

        //The branch serves the files static files serve only without an endpoint (none is served while offline).
        context.SetEndpoint(null);
        return _offlineBranch(context);
    }

    /// <summary>The last step of the offline branch: 503 with the offline file's content.</summary>
    public static async Task WritePageAsync(HttpContext context, OfflineFile file)
    {
        var page = file.Get();
        var response = context.Response;
        response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        response.Headers.CacheControl = NoStore;
        context.Items[FrontendServing.FrontendFileItem] = true;
        if (page == null)
        {
            //Removed in the meantime; the backend was offline when the request arrived.
            return;
        }

        response.ContentType = PageContentType;
        response.ContentLength = page.Length;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        try
        {
            await response.SendFileAsync(page, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException || (e is OperationCanceledException && context.RequestAborted.IsCancellationRequested))
        {
            //Removed while it was being sent, or the client went away.
        }
    }
}

/// <summary>
/// The offline file at the web root's top, looked up through the web root's file provider (the
/// host's, or a <see cref="LateWebRootFileProvider"/> when the web root did not exist at
/// start-up).
/// </summary>
internal sealed class OfflineFile
{
    private readonly IFileProvider _webRoot;

    /// <summary>The offline file of a web root.</summary>
    /// <param name="webRoot">The web root's files.</param>
    public OfflineFile(IFileProvider webRoot)
    {
        _webRoot = webRoot ?? throw new ArgumentNullException(nameof(webRoot));
    }

    /// <summary>True when the offline file is there.</summary>
    public bool IsPresent() => Get() != null;

    /// <summary>The offline file, or null when it is not there.</summary>
    public IFileInfo Get()
    {
        var info = _webRoot.GetFileInfo("/" + PlatformBackendPaths.OfflineFileName);
        return info.Exists && !info.IsDirectory ? info : null;
    }
}
