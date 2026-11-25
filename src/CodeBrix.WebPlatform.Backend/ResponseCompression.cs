namespace CodeBrix.WebPlatform.Backend;

/// <summary>
/// Which answers the backend compresses as it sends them
/// (<see cref="PlatformBackendOptions.CompressResponses"/>). The built frontend files are never
/// among them: those are compressed once, when the frontend is built, and served as they are.
/// </summary>
public enum ResponseCompression
{
    /// <summary>Nothing is compressed as it is sent (the default).</summary>
    None,

    /// <summary>
    /// The answers of actions marked <c>[WireFile]</c> are compressed, and nothing else. The
    /// matched endpoint decides, per request: an action without the attribute that answers with
    /// a file (<c>File(...)</c>, <c>PhysicalFile(...)</c>) or with text is not compressed. A
    /// <c>[WireFile]</c> answer is compressed when its type is JSON or is in
    /// <see cref="PlatformBackendOptions.CompressedFileTypes"/>; types that are compressed
    /// already (images, archives, PDF, WOFF fonts, audio, video) are not in that set by default.
    /// </summary>
    FileAnswers,

    /// <summary>
    /// The answers of <c>[WireFile]</c> actions as with <see cref="FileAnswers"/>, and the JSON
    /// answers of every action too (<c>application/json</c>, <c>application/problem+json</c> and
    /// every other JSON type). Other answers of actions without the attribute are not compressed.
    /// </summary>
    FileAnswersAndJson,
}
