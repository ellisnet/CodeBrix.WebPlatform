namespace CodeBrix.WebPlatform.Core.Problems;

/// <summary>
/// The values of <see cref="ApiProblem.Code"/> that the platform itself defines. The frontend's
/// shared error type assumes these when an answer carries no code of its own.
/// </summary>
public static class ApiProblemCodes
{
    /// <summary>400: one or more fields are invalid.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>400: a state-changing request lacks the <c>X-Requested-With</c> header.</summary>
    public const string MissingRequestHeader = "missing_request_header";

    /// <summary>401: sign-in with a wrong identifier or password.</summary>
    public const string InvalidCredentials = "invalid_credentials";

    /// <summary>404: no such resource, or no such API route.</summary>
    public const string NotFound = "not_found";

    /// <summary>409: a duplicate value, or a change blocked by dependent data.</summary>
    public const string Conflict = "conflict";

    /// <summary>429: too many requests.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>Any 5xx answer that carries no code of its own.</summary>
    public const string ServerError = "server_error";
}
