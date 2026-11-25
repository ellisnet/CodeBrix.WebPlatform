using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.StaticFiles;

namespace CodeBrix.WebPlatform.Backend;

/// <summary>
/// What <see cref="PlatformBackendApplicationExtensions.UsePlatformBackend(Microsoft.AspNetCore.Builder.WebApplication)"/>
/// and <see cref="PlatformBackendServiceCollectionExtensions.AddPlatformBackend"/> do. Every
/// piece is on by default and can be switched off on its own; the one value an application
/// normally sets is <see cref="ContentSecurityPolicy"/>.
/// </summary>
public sealed class PlatformBackendOptions
{
    /// <summary>The default of <see cref="HashedFileCacheControl"/>.</summary>
    public const string DefaultHashedFileCacheControl = "public, max-age=31536000, immutable";

    /// <summary>The default of <see cref="UnhashedFileCacheControl"/>.</summary>
    public const string DefaultUnhashedFileCacheControl = "no-cache";

    /// <summary>
    /// Serve the frontend applications from the built frontend (<c>frontend-built/</c> in the
    /// content root, <see cref="BuiltFrontend.FolderName"/>), with their single-page-app
    /// fallbacks, and the developer's own files from the web root (<c>wwwroot/</c>, when it
    /// exists). The frontend manifest, the built frontend's <c>.gitignore</c> and the offline
    /// file are never served. Default true.
    /// </summary>
    public bool ServeFrontendApplications { get; set; } = true;

    /// <summary>
    /// Which file is served when an address names a file in both <c>frontend-built/</c> and
    /// <c>wwwroot/</c>. Default <see cref="FrontendFilePrecedence.BuiltFrontendWins"/>. The
    /// backend logs one warning at start-up naming the clashing addresses, in both modes.
    /// </summary>
    public FrontendFilePrecedence FilePrecedence { get; set; } = FrontendFilePrecedence.BuiltFrontendWins;

    /// <summary>
    /// Serve the compressed copies the frontend build writes beside the built frontend's files:
    /// a GET or HEAD of a file (or of an application's index.html through its fallback) whose
    /// <c>Accept-Encoding</c> allows it is answered with the file's <c>.br</c> copy (preferred)
    /// or <c>.gz</c> copy, with <c>Content-Encoding</c>, the file's own <c>Content-Type</c> and
    /// <c>Vary: Accept-Encoding</c>. A request with a <c>Range</c> header gets the file itself.
    /// The copies are never served under their own names, whatever this says. Only the built
    /// frontend has copies: a <c>.br</c> / <c>.gz</c> file in <c>wwwroot/</c> is an ordinary
    /// file. Harmless when there are no copies. Default true.
    /// </summary>
    public bool ServePrecompressedFiles { get; set; } = true;

    /// <summary>
    /// The <c>Cache-Control</c> sent with a built frontend's file whose name carries the content
    /// hash dx gives it (<c>&lt;stem&gt;-dxh&lt;8 to 16 lowercase hex digits&gt;</c>, then the
    /// extension, for example <c>app_bg-dxh4e7e6d433fe816da.wasm</c>): such a file never changes
    /// under its name. Default <see cref="DefaultHashedFileCacheControl"/>. Null or blank sends
    /// no header. It must be one line.
    /// </summary>
    public string HashedFileCacheControl { get; set; } = DefaultHashedFileCacheControl;

    /// <summary>
    /// The <c>Cache-Control</c> sent with every other file: the built frontend's files whose
    /// names carry no content hash (each application's <c>index.html</c>, by its address or
    /// through its fallback, included) and every file of <c>wwwroot/</c>, whatever its name.
    /// Default <see cref="DefaultUnhashedFileCacheControl"/> (the browser may keep the file but
    /// asks each time whether it changed). Null or blank sends no header. It must be one line.
    /// </summary>
    public string UnhashedFileCacheControl { get; set; } = DefaultUnhashedFileCacheControl;

    /// <summary>
    /// The content type of a served file, by its extension (with the dot, any casing). A file
    /// whose extension is not in the table is not served (404). Pre-filled with ASP.NET Core's
    /// table (<see cref="FileExtensionContentTypeProvider"/>) and the platform's corrections and
    /// additions: <c>.wasm</c> <c>application/wasm</c>, <c>.ttf</c> <c>font/ttf</c>, <c>.otf</c>
    /// <c>font/otf</c>, <c>.hdr</c> <c>image/vnd.radiance</c>, <c>.glb</c>
    /// <c>model/gltf-binary</c>, <c>.gltf</c> <c>model/gltf+json</c>, <c>.wgsl</c>
    /// <c>text/wgsl</c> (<c>.bin</c> is <c>application/octet-stream</c> in ASP.NET Core's
    /// table). A <c>text/</c> type without a charset is served with <c>; charset=utf-8</c>
    /// added. Change it in the options call; it is read once, by the pipeline call.
    /// </summary>
    public IDictionary<string, string> StaticFileContentTypes { get; } = CreateStaticFileContentTypes();

    /// <summary>
    /// The content types a <c>[WireFile]</c> answer is compressed for when
    /// <see cref="CompressResponses"/> is not <see cref="ResponseCompression.None"/>. An entry is
    /// a type (<c>application/wasm</c>), every subtype of a type (<c>text/*</c>), or every type
    /// with a structured-syntax suffix (<c>*/*+xml</c>, or <c>image/*+xml</c>); any casing.
    /// Pre-filled with <c>text/*</c>, <c>*/*+xml</c>, <c>application/octet-stream</c>,
    /// <c>application/wasm</c>, <c>application/javascript</c>, <c>application/x-javascript</c>,
    /// <c>application/xml</c>, <c>application/yaml</c>, <c>application/x-yaml</c>,
    /// <c>application/toml</c>, the TrueType and OpenType font types (<c>font/ttf</c>,
    /// <c>font/otf</c>, <c>font/sfnt</c>, <c>application/x-font-ttf</c>,
    /// <c>application/x-font-otf</c>, <c>application/font-sfnt</c>,
    /// <c>application/vnd.ms-opentype</c>), <c>image/vnd.radiance</c> and
    /// <c>model/gltf-binary</c>. WOFF and WOFF2 fonts, images, archives, audio and video are
    /// compressed already and are not in it. Whatever the set holds, JSON answers follow
    /// <see cref="CompressResponses"/> alone and <c>text/event-stream</c> is never compressed.
    /// </summary>
    public ISet<string> CompressedFileTypes { get; } = new HashSet<string>(DefaultCompressedFileTypes, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Compress answers the backend produces as they are sent (Brotli when the browser accepts
    /// it, otherwise gzip, at a fast setting, over HTTPS too). Default
    /// <see cref="ResponseCompression.None"/>: those answers can hold a user's data, and
    /// compressing over HTTPS an answer that mixes a secret with text an attacker controls can
    /// leak the secret (the BREACH class of attack), so the developer decides.
    /// <see cref="ResponseCompression.FileAnswers"/> compresses the answers of <c>[WireFile]</c>
    /// actions whose type is in <see cref="CompressedFileTypes"/> (and their JSON answers), and
    /// nothing else (the matched endpoint decides, per request);
    /// <see cref="ResponseCompression.FileAnswersAndJson"/> JSON answers too. Hub traffic, the
    /// served files and <c>text/event-stream</c> answers are never compressed this way.
    /// </summary>
    public ResponseCompression CompressResponses { get; set; } = ResponseCompression.None;

    /// <summary>
    /// Answer every address below <c>/api</c> that no controller or hub answers with 404 and the
    /// standard error body (code <c>not_found</c>), for every HTTP method. Default true.
    /// </summary>
    public bool AnswerUnknownApiRoutes { get; set; } = true;

    /// <summary>
    /// Give the standard error body to every 4xx answer below <c>/api</c> that the framework
    /// produced without a body (a 415 for a body that is not JSON, a controller's bare
    /// <c>NotFound()</c>). A 401 stays empty and a 5xx is left alone. Also stops MVC from
    /// writing its own problem details for such answers. Default true.
    /// </summary>
    public bool AddProblemBodiesToStatusCodes { get; set; } = true;

    /// <summary>
    /// Answer MVC model-binding failures (malformed JSON, a value that cannot be read) with 400
    /// <c>validation_failed</c> and one message per field. Default true.
    /// </summary>
    public bool AnswerModelBindingFailures { get; set; } = true;

    /// <summary>
    /// Refuse every POST, PUT, PATCH and DELETE below <c>/api</c> that does not carry the
    /// backend's request marker in <c>X-Requested-With</c> (400 <c>missing_request_header</c>).
    /// The marker is <see cref="RequestMarker"/>, otherwise the
    /// <c>[assembly: WireRequestMarker]</c> value of <see cref="BackendAssembly"/>, otherwise
    /// that of the application's assembly (the one named by the host's application name). A
    /// backend that declares none fails at start-up while this is on. Default true.
    /// </summary>
    public bool RequireRequestMarker { get; set; } = true;

    /// <summary>
    /// Refuse every request below <c>/api/hubs</c> whose <c>Origin</c> header is not
    /// this site's own scheme and host, compared without regard to case, or one of
    /// <see cref="AdditionalHubOrigins"/> (403 <c>forbidden_origin</c>), public hubs included. Default true.
    /// </summary>
    public bool RequireHubOrigin { get; set; } = true;

    /// <summary>
    /// Origins other than the site's own whose pages may use the hubs while
    /// <see cref="RequireHubOrigin"/> is on: typically the dx dev server's
    /// (<c>http://localhost:8080</c>) while a frontend is served with dx and its
    /// <c>[[web.proxy]]</c> sends <c>/api</c> to the backend, and normally only in Development.
    /// Each entry is an http or https origin - scheme, host and, when it is not the default, the
    /// port; a trailing slash is allowed - matched exactly on scheme, host and port, without
    /// regard to case (<c>HTTP://LocalHost:80</c> is <c>http://localhost</c>); no wildcards. An
    /// entry with a path, a query, a wildcard or no scheme makes the pipeline call throw. Empty
    /// by default. Ignored while <see cref="RequireHubOrigin"/> is off.
    /// </summary>
    public IList<string> AdditionalHubOrigins { get; } = new List<string>();

    /// <summary>
    /// Send <c>X-Content-Type-Options: nosniff</c>, <c>X-Frame-Options: DENY</c> and
    /// <c>Referrer-Policy: no-referrer</c> on every answer, and
    /// <c>Content-Security-Policy</c> when <see cref="ContentSecurityPolicy"/> is set. Default true.
    /// </summary>
    public bool SendSecurityHeaders { get; set; } = true;

    /// <summary>
    /// The <c>Content-Security-Policy</c> sent on every answer, exactly as given. The platform has
    /// no default: when this is null or blank the header is not sent. It must be one line.
    /// </summary>
    public string ContentSecurityPolicy { get; set; }

    /// <summary>
    /// The value demanded in <c>X-Requested-With</c>, when it is not to be read from an
    /// assembly. Normally left null: the value is read from the backend's
    /// <c>[assembly: WireRequestMarker]</c>, the one the generated frontend code sends. It must
    /// pass <see cref="WireRequestMarkerAttribute.IsValidValue(string)"/>.
    /// </summary>
    public string RequestMarker { get; set; }

    /// <summary>
    /// The backend assembly whose <c>[assembly: WireRequestMarker]</c> is demanded. Normally left
    /// null: the application's own assembly (the host's application name, which is also the
    /// assembly a test host built from <c>WebApplicationFactory&lt;Program&gt;</c> names). Set it
    /// when the marker is declared on another assembly, for example
    /// <c>typeof(Program).Assembly</c>.
    /// </summary>
    public Assembly BackendAssembly { get; set; }

    private static readonly string[] DefaultCompressedFileTypes =
    {
        "text/*", "*/*+xml", "application/octet-stream", "application/wasm", "application/javascript",
        "application/x-javascript", "application/xml", "application/yaml", "application/x-yaml", "application/toml",
        "font/ttf", "font/otf", "font/sfnt", "application/x-font-ttf", "application/x-font-otf", "application/font-sfnt",
        "application/vnd.ms-opentype", "image/vnd.radiance", "model/gltf-binary",
    };

    private static Dictionary<string, string> CreateStaticFileContentTypes()
    {
        var table = new Dictionary<string, string>(new FileExtensionContentTypeProvider().Mappings, StringComparer.OrdinalIgnoreCase)
        {
            [".wasm"] = "application/wasm",
            [".ttf"] = "font/ttf",
            [".otf"] = "font/otf",
            [".hdr"] = "image/vnd.radiance",
            [".glb"] = "model/gltf-binary",
            [".gltf"] = "model/gltf+json",
            [".wgsl"] = "text/wgsl",
        };
        table.TryAdd(".bin", "application/octet-stream");
        return table;
    }
}
