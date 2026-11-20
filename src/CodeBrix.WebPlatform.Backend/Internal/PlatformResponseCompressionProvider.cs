using System;
using System.IO.Compression;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>
/// Decides, for ASP.NET Core's response-compression middleware, which answers are compressed as
/// they are sent (<see cref="PlatformBackendOptions.CompressResponses"/>): the answers of the
/// actions marked <c>[WireFile]</c> (read from the matched endpoint's metadata, per request), and
/// with <see cref="ResponseCompression.FileAnswersAndJson"/> the JSON answers too. Never a
/// web-root file (those are compressed when the frontend is built), never hub traffic, never
/// <c>text/event-stream</c>, never a type that is compressed already, never an answer that
/// already has a <c>Content-Encoding</c> or a <c>Content-Range</c>. Brotli when the request
/// prefers it, otherwise gzip, both at their fastest setting, over HTTPS too. The coding is
/// chosen by <see cref="ContentCodings"/>, as for the precompressed files.
/// </summary>
internal sealed class PlatformResponseCompressionProvider : IResponseCompressionProvider
{
    private readonly ResponseCompression _mode;
    private readonly ResponseCompressionProvider _framework;
    private readonly ICompressionProvider _brotli;
    private readonly ICompressionProvider _gzip;

    public PlatformResponseCompressionProvider(IServiceProvider services, ResponseCompression mode)
    {
        _mode = mode;
        _brotli = new BrotliCompressionProvider(Options.Create(new BrotliCompressionProviderOptions { Level = CompressionLevel.Fastest }));
        _gzip = new GzipCompressionProvider(Options.Create(new GzipCompressionProviderOptions { Level = CompressionLevel.Fastest }));
        var options = new ResponseCompressionOptions { EnableForHttps = true, MimeTypes = new[] { "*/*" } };
        options.Providers.Add(_brotli);
        options.Providers.Add(_gzip);
        //The framework's own checks (HTTPS opt-out, Content-Range, Content-Encoding, no content type) run after ours.
        _framework = new ResponseCompressionProvider(services, Options.Create(options));
    }

    public ICompressionProvider GetCompressionProvider(HttpContext context)
        => ContentCodings.Choose(context.Request.Headers.AcceptEncoding, true, true) switch
        {
            ContentCodings.Brotli => _brotli,
            ContentCodings.Gzip => _gzip,
            _ => null,
        };

    public bool CheckRequestAcceptsCompression(HttpContext context)
        => ContentCodings.Choose(context.Request.Headers.AcceptEncoding, true, true) != null;

    public bool ShouldCompressResponse(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (context.Items.ContainsKey(FrontendServing.FrontendFileItem)
            || context.Request.Path.StartsWithSegments(PlatformBackendPaths.Hubs, StringComparison.OrdinalIgnoreCase)
            || endpoint?.Metadata.GetMetadata<HubMetadata>() != null
            || !IsCompressedAnswer(context.Response.ContentType, endpoint?.Metadata.GetMetadata<WireFileAttribute>() != null, _mode))
        {
            return false;
        }

        return _framework.ShouldCompressResponse(context);
    }

    /// <summary>
    /// Whether an answer is compressed in a mode: an answer of a <c>[WireFile]</c> action of any
    /// type that is not compressed already, with <see cref="ResponseCompression.FileAnswersAndJson"/>
    /// also a JSON answer of any action, and nothing else.
    /// </summary>
    internal static bool IsCompressedAnswer(string contentType, bool wireFileAnswer, ResponseCompression mode)
    {
        if (mode == ResponseCompression.None || string.IsNullOrEmpty(contentType)
            || !MediaTypeHeaderValue.TryParse(contentType, out var parsed) || !parsed.MediaType.HasValue)
        {
            return false;
        }

        var type = parsed.MediaType.Value.ToLowerInvariant();
        if (wireFileAnswer)
        {
            return IsJson(type) || IsCompressibleFileType(type);
        }

        return mode == ResponseCompression.FileAnswersAndJson && IsJson(type);
    }

    private static bool IsCompressibleFileType(string type)
    {
        if (type == "text/event-stream")
        {
            return false;
        }

        return type.StartsWith("text/", StringComparison.Ordinal)
            || type == "application/octet-stream"
            || type == "application/wasm"
            || type == "application/javascript"
            || type == "application/x-javascript"
            || type == "application/xml"
            || type.EndsWith("+xml", StringComparison.Ordinal)
            || type == "application/yaml"
            || type == "application/x-yaml"
            || type == "application/toml";
    }

    private static bool IsJson(string type)
        => type == "application/json" || type == "text/json" || type.EndsWith("+json", StringComparison.Ordinal)
            || type == "application/x-ndjson" || type == "application/ndjson";
}
