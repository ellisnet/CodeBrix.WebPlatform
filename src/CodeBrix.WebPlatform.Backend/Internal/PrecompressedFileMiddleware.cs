using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Answers a GET or HEAD of a web-root file with the file's compressed copy (<c>.br</c>, else
/// <c>.gz</c>) when the request's <c>Accept-Encoding</c> prefers it, just before the static
/// files (in the main pipeline and in each application's fallback). The answer carries
/// <c>Content-Encoding</c>, the file's own <c>Content-Type</c>, the copy's
/// <c>Content-Length</c>, <c>Last-Modified</c> and <c>ETag</c>, and honours the conditional
/// headers (304, 412). Ranges are not offered for a copy: a request with a <c>Range</c> header
/// is handed on to the static files, which answer it from the file itself. Every answer for a
/// file that has a copy - compressed or not - carries <c>Vary: Accept-Encoding</c>. Anything
/// else goes on unchanged.
/// </summary>
internal sealed class PrecompressedFileMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ManifestHidingFileProvider _files;
    private readonly IContentTypeProvider _contentTypes;

    public PrecompressedFileMiddleware(RequestDelegate next, ManifestHidingFileProvider files, IContentTypeProvider contentTypes)
    {
        _next = next;
        _files = files;
        _contentTypes = contentTypes;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if ((!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            || context.GetEndpoint()?.RequestDelegate != null)
        {
            return _next(context);
        }

        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path) || path.EndsWith('/') || !_contentTypes.TryGetContentType(path, out var contentType))
        {
            return _next(context);
        }

        var file = _files.GetFileInfo(path);
        if (!file.Exists || file.IsDirectory)
        {
            return _next(context);
        }

        var brotli = _files.GetCopy(path, ".br");
        var gzip = _files.GetCopy(path, ".gz");
        if (!brotli.Exists && !gzip.Exists)
        {
            return _next(context);
        }

        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);
        if (!string.IsNullOrEmpty(request.Headers.Range))
        {
            return _next(context);
        }

        var coding = ContentCodings.Choose(request.Headers.AcceptEncoding, brotli.Exists, gzip.Exists);
        if (coding == null)
        {
            return _next(context);
        }

        return ServeAsync(context, coding == ContentCodings.Brotli ? brotli : gzip, coding, contentType);
    }

    private static async Task ServeAsync(HttpContext context, IFileInfo copy, string coding, string contentType)
    {
        var length = copy.Length;
        var modified = copy.LastModified;
        //Whole seconds, as HTTP dates have them (and as the static files do it).
        var lastModified = new DateTimeOffset(modified.Year, modified.Month, modified.Day, modified.Hour, modified.Minute, modified.Second, modified.Offset).ToUniversalTime();
        var etag = new EntityTagHeaderValue("\"" + Convert.ToString(lastModified.ToFileTime() ^ length, 16) + "\"");

        var response = context.Response;
        var headers = response.GetTypedHeaders();
        context.Items[FrontendServing.FrontendFileItem] = true;
        var status = Precondition(context.Request.GetTypedHeaders(), etag, lastModified);
        if (status == StatusCodes.Status412PreconditionFailed)
        {
            response.StatusCode = status;
            return;
        }

        headers.LastModified = lastModified;
        headers.ETag = etag;
        if (status == StatusCodes.Status304NotModified)
        {
            response.StatusCode = status;
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = contentType;
        response.Headers.ContentEncoding = coding;
        response.ContentLength = length;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        try
        {
            await response.SendFileAsync(copy, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            //The client went away; there is nobody to answer.
        }
    }

    /// <summary>
    /// The conditional headers, in the order of RFC 9110 section 13.2.2: If-Match (else
    /// If-Unmodified-Since) can fail the request with 412; If-None-Match (else
    /// If-Modified-Since) can answer 304. 200 when the copy is to be sent.
    /// </summary>
    internal static int Precondition(RequestHeaders request, EntityTagHeaderValue etag, DateTimeOffset lastModified)
    {
        var ifMatch = request.IfMatch;
        if (ifMatch != null && ifMatch.Count > 0)
        {
            var matched = false;
            foreach (var candidate in ifMatch)
            {
                if (candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(etag, useStrongComparison: true))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return StatusCodes.Status412PreconditionFailed;
            }
        }
        else if (request.IfUnmodifiedSince is { } unmodifiedSince && lastModified > unmodifiedSince)
        {
            return StatusCodes.Status412PreconditionFailed;
        }

        var ifNoneMatch = request.IfNoneMatch;
        if (ifNoneMatch != null && ifNoneMatch.Count > 0)
        {
            foreach (var candidate in ifNoneMatch)
            {
                if (candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(etag, useStrongComparison: false))
                {
                    return StatusCodes.Status304NotModified;
                }
            }
        }
        else if (request.IfModifiedSince is { } modifiedSince && modifiedSince <= DateTimeOffset.UtcNow && lastModified <= modifiedSince)
        {
            return StatusCodes.Status304NotModified;
        }

        return StatusCodes.Status200OK;
    }
}
