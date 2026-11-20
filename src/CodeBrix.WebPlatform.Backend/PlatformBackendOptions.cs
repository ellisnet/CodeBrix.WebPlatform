using System.Reflection;
using CodeBrix.WebPlatform.Core.Mapping;

namespace CodeBrix.WebPlatform.Backend;

/// <summary>
/// What <see cref="PlatformBackendApplicationExtensions.UsePlatformBackend(Microsoft.AspNetCore.Builder.WebApplication)"/>
/// and <see cref="PlatformBackendServiceCollectionExtensions.AddPlatformBackend"/> do. Every
/// piece is on by default and can be switched off on its own; the one value an application
/// normally sets is <see cref="ContentSecurityPolicy"/>.
/// </summary>
public sealed class PlatformBackendOptions
{
    /// <summary>
    /// Serve the web root's static files (<c>.wasm</c> as <c>application/wasm</c>) and the
    /// frontend applications, with their single-page-app fallbacks. The frontend manifest is
    /// never served. Default true.
    /// </summary>
    public bool ServeFrontendApplications { get; set; } = true;

    /// <summary>
    /// Serve the compressed copies the frontend build writes beside the web root's files: a GET
    /// or HEAD of a file (or of an application's index.html through its fallback) whose
    /// <c>Accept-Encoding</c> allows it is answered with the file's <c>.br</c> copy (preferred)
    /// or <c>.gz</c> copy, with <c>Content-Encoding</c>, the file's own <c>Content-Type</c> and
    /// <c>Vary: Accept-Encoding</c>. A request with a <c>Range</c> header gets the file itself.
    /// The copies are never served under their own names, whatever this says. Harmless when
    /// there are no copies. Default true.
    /// </summary>
    public bool ServePrecompressedFiles { get; set; } = true;

    /// <summary>
    /// Compress answers the backend produces as they are sent (Brotli when the browser accepts
    /// it, otherwise gzip, at a fast setting, over HTTPS too). Default
    /// <see cref="ResponseCompression.None"/>: those answers can hold a user's data, and
    /// compressing over HTTPS an answer that mixes a secret with text an attacker controls can
    /// leak the secret (the BREACH class of attack), so the developer decides.
    /// <see cref="ResponseCompression.FileAnswers"/> compresses the answers of <c>[WireFile]</c>
    /// actions, and nothing else (the matched endpoint decides, per request);
    /// <see cref="ResponseCompression.FileAnswersAndJson"/> JSON answers too. Hub traffic, the
    /// web root's files and <c>text/event-stream</c> answers are never compressed this way.
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
    /// this site's own scheme and host, compared without regard to case (403 <c>forbidden_origin</c>), public hubs included. Default true.
    /// </summary>
    public bool RequireHubOrigin { get; set; } = true;

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
}
