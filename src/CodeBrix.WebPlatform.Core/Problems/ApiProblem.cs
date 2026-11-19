using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeBrix.WebPlatform.Core.Problems;

/// <summary>
/// The body of every error answer a platform backend sends: a sentence fit to show, the status
/// code, a stable machine-readable code and, for invalid input, messages per field. It is written
/// as <c>application/problem+json</c> with camelCase names. The frontend's shared error type
/// reads exactly this shape.
/// </summary>
/// <remarks>
/// The one error answer without this body is a 401 that means "not signed in" (or "the session
/// has ended"): it is sent with an empty body.
/// </remarks>
public sealed class ApiProblem
{
    /// <summary>The media type of the body.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>A sentence fit to show the user.</summary>
    public string Title { get; set; }

    /// <summary>The HTTP status code of the answer.</summary>
    public int Status { get; set; }

    /// <summary>
    /// A stable code the frontend can branch on. The platform's own values are in
    /// <see cref="ApiProblemCodes"/>; an application may add its own.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Messages per field, keyed by the camelCase field name. Left out of the body when null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]> Errors { get; set; }
}
